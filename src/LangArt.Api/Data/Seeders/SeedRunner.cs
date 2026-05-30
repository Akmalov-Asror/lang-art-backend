using System.Text.Json;
using LangArt.Api.Common.Configuration;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Data.Seeders;

/// <summary>
/// Invoked from Program.cs when the host is launched with one of:
///   dotnet run -- seed     creates the default admin + sample teachers/students/courses
///   dotnet run -- clear    truncates seeded tables
///   dotnet run -- reset    clear then seed
/// </summary>
public static class SeedRunner
{
    private const string StudentPassword = "password123";

    public static async Task<int> RunAsync(string command, IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        switch (command.ToLowerInvariant())
        {
            case "seed":
                await SeedAsync(db, seed, logger);
                break;
            case "clear":
                await ClearAsync(db, logger);
                break;
            case "reset":
                await ClearAsync(db, logger);
                await SeedAsync(db, seed, logger);
                break;
            case "seed:test-english":
            {
                await EnsureSchemaUpgradesAsync(db);
                var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                await TestEnglishSeeder.RunAsync(db, env, logger, force: false);
                break;
            }
            case "seed:test-english:force":
            {
                await EnsureSchemaUpgradesAsync(db);
                var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                await TestEnglishSeeder.RunAsync(db, env, logger, force: true);
                break;
            }
            case "clear:non-grammar":
            {
                await EnsureSchemaUpgradesAsync(db);
                await ClearNonGrammarCoursesAsync(db, logger);
                break;
            }
            default:
                logger.LogError(
                    "Unknown seed command: {Cmd}. Valid: seed | clear | reset | seed:test-english | seed:test-english:force | clear:non-grammar",
                    command);
                return 1;
        }

        return 0;
    }

    /// <summary>
    /// Lightweight migration runner — applied on every seed/reset so schema.sql + new
    /// columns we add over time stay in sync without a full EF migrations setup.
    /// </summary>
    public static async Task EnsureSchemaUpgradesAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            -- Phase 5.4: teacher-owned courses
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS owner_id uuid;
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE table_name = 'courses' AND constraint_name = 'courses_owner_id_fkey'
                ) THEN
                    ALTER TABLE courses
                        ADD CONSTRAINT courses_owner_id_fkey
                        FOREIGN KEY (owner_id) REFERENCES profiles(id) ON DELETE SET NULL;
                END IF;
            END$$;
            CREATE INDEX IF NOT EXISTS ix_courses_owner_id ON courses (owner_id);

            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS avatar_url text;
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS totp_secret text;
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS totp_enabled boolean NOT NULL DEFAULT false;
            CREATE TABLE IF NOT EXISTS notifications (
                id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id       uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                kind          text NOT NULL,
                title         text NOT NULL,
                body          text,
                link_url      text,
                read_at       timestamptz,
                created_at    timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS idx_notifications_user_unread
                ON notifications(user_id) WHERE read_at IS NULL;

            CREATE TABLE IF NOT EXISTS live_sessions (
                id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                classroom_id         uuid NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
                lesson_id            uuid NOT NULL REFERENCES lessons(id) ON DELETE RESTRICT,
                teacher_id           uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
                started_at           timestamptz NOT NULL DEFAULT now(),
                ended_at             timestamptz,
                current_block_index  integer NOT NULL DEFAULT 0,
                end_reason           text
            );
            CREATE INDEX IF NOT EXISTS ix_live_sessions_classroom_ended_at
                ON live_sessions (classroom_id, ended_at);
            CREATE INDEX IF NOT EXISTS ix_live_sessions_teacher_started_at
                ON live_sessions (teacher_id, started_at DESC);
            CREATE UNIQUE INDEX IF NOT EXISTS ix_live_sessions_one_active_per_classroom
                ON live_sessions (classroom_id) WHERE ended_at IS NULL;

            -- ===== Sprint 1: Gamification =====
            CREATE TABLE IF NOT EXISTS user_xp (
                user_id     uuid PRIMARY KEY REFERENCES profiles(id) ON DELETE CASCADE,
                total_xp    integer NOT NULL DEFAULT 0,
                updated_at  timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS user_streaks (
                user_id                  uuid PRIMARY KEY REFERENCES profiles(id) ON DELETE CASCADE,
                current_streak           integer NOT NULL DEFAULT 0,
                longest_streak           integer NOT NULL DEFAULT 0,
                last_activity_date_utc   date    NOT NULL DEFAULT CURRENT_DATE
            );

            CREATE TABLE IF NOT EXISTS badges (
                id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                code         text NOT NULL UNIQUE,
                name         text NOT NULL,
                description  text NOT NULL DEFAULT '',
                icon_url     text,
                criteria     jsonb NOT NULL DEFAULT '{{}}'::jsonb,
                xp_reward    integer NOT NULL DEFAULT 0,
                created_at   timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS user_badges (
                user_id        uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                badge_id       uuid NOT NULL REFERENCES badges(id)   ON DELETE CASCADE,
                earned_at_utc  timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (user_id, badge_id)
            );

            CREATE TABLE IF NOT EXISTS xp_ledger (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id         uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                amount          integer NOT NULL,
                reason          text NOT NULL CHECK (reason IN (
                                   'lesson_completed', 'quiz_passed', 'quiz_perfect_bonus',
                                   'daily_login', 'streak_bonus', 'badge_reward', 'admin_adjustment'
                                )),
                source_id       uuid,
                created_at_utc  timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_xp_ledger_user_created
                ON xp_ledger (user_id, created_at_utc DESC);

            -- Idempotency for source-bound XP awards (e.g. one lesson completion = one award).
            CREATE UNIQUE INDEX IF NOT EXISTS uq_xp_ledger_source
                ON xp_ledger (user_id, reason, source_id)
                WHERE source_id IS NOT NULL;

            -- Idempotency for the once-per-day DailyLogin reward (source_id is null).
            CREATE UNIQUE INDEX IF NOT EXISTS uq_xp_ledger_daily_login
                ON xp_ledger (user_id, ((created_at_utc AT TIME ZONE 'UTC')::date))
                WHERE reason = 'daily_login';

            -- ===== Sprint 1, Phase B: Push notifications =====
            CREATE TABLE IF NOT EXISTS push_subscriptions (
                id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id      uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                endpoint     text NOT NULL UNIQUE,
                p256dh       text NOT NULL,
                auth         text NOT NULL,
                user_agent   text,
                created_at_utc  timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_push_subscriptions_user ON push_subscriptions (user_id);

            -- ===== Phase 1: Vocabulary system =====
            CREATE TABLE IF NOT EXISTS wordlists (
                id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                owner_id     uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                name         text NOT NULL,
                description  text,
                is_public    boolean NOT NULL DEFAULT false,
                level        text NOT NULL DEFAULT 'A1',
                created_at   timestamptz NOT NULL DEFAULT now(),
                updated_at   timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_wordlists_owner ON wordlists (owner_id);
            CREATE INDEX IF NOT EXISTS ix_wordlists_public_level ON wordlists (is_public, level);

            CREATE TABLE IF NOT EXISTS words (
                id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                wordlist_id        uuid NOT NULL REFERENCES wordlists(id) ON DELETE CASCADE,
                term               text NOT NULL,
                translation_uz     text NOT NULL DEFAULT '',
                translation_ru     text NOT NULL DEFAULT '',
                definition         text NOT NULL DEFAULT '',
                pronunciation_url  text,
                example_sentence   text,
                part_of_speech     text,
                position           integer NOT NULL DEFAULT 0,
                created_at         timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_words_wordlist ON words (wordlist_id);

            CREATE TABLE IF NOT EXISTS user_wordlist_entries (
                id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id             uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                word_id             uuid NOT NULL REFERENCES words(id)    ON DELETE CASCADE,
                status              text NOT NULL DEFAULT 'new' CHECK (status IN ('new','learning','learned')),
                last_reviewed_at    timestamptz,
                correct_count       integer NOT NULL DEFAULT 0,
                incorrect_count     integer NOT NULL DEFAULT 0,
                added_at            timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_user_wordlist_entry_user_word
                ON user_wordlist_entries (user_id, word_id);
            CREATE INDEX IF NOT EXISTS ix_user_wordlist_entries_user_status
                ON user_wordlist_entries (user_id, status);

            -- Extend xp_ledger CHECK constraint as new reasons are added over time.
            -- Drop any older reason CHECK and add the current full set under a fresh name.
            DO $$
            DECLARE
                v_name text;
            BEGIN
                FOR v_name IN
                    SELECT constraint_name
                    FROM information_schema.table_constraints
                    WHERE table_name = 'xp_ledger'
                      AND constraint_type = 'CHECK'
                      AND constraint_name LIKE '%reason%'
                      AND constraint_name != 'xp_ledger_reason_check_v5'
                LOOP
                    EXECUTE 'ALTER TABLE xp_ledger DROP CONSTRAINT ' || quote_ident(v_name);
                END LOOP;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE table_name = 'xp_ledger' AND constraint_name = 'xp_ledger_reason_check_v5'
                ) THEN
                    ALTER TABLE xp_ledger ADD CONSTRAINT xp_ledger_reason_check_v5 CHECK (reason IN (
                        'lesson_completed', 'quiz_passed', 'quiz_perfect_bonus',
                        'daily_login', 'streak_bonus', 'badge_reward', 'admin_adjustment',
                        'vocabulary_mastered', 'reading_completed', 'speaking_completed',
                        'writing_completed'
                    ));
                END IF;
            END$$;

            -- ===== Phase 2: Multilingual content translations =====
            CREATE TABLE IF NOT EXISTS lesson_content_translations (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                content_id      uuid NOT NULL REFERENCES lesson_content(id) ON DELETE CASCADE,
                language        text NOT NULL CHECK (language IN ('uz','ru','en')),
                body_markdown   text NOT NULL DEFAULT '',
                video_url       text,
                subtitle_url    text,
                script          text,
                created_at      timestamptz NOT NULL DEFAULT now(),
                updated_at      timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_lesson_content_translation_content_lang
                ON lesson_content_translations (content_id, language);

            -- ===== Phase 4: LA Dollar currency =====
            CREATE TABLE IF NOT EXISTS user_la_dollar_balances (
                user_id        uuid PRIMARY KEY REFERENCES profiles(id) ON DELETE CASCADE,
                total_balance  integer NOT NULL DEFAULT 0,
                updated_at     timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS la_dollar_ledger (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id         uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                amount          integer NOT NULL,
                reason          text NOT NULL,
                source_id       uuid,
                description     text,
                created_at_utc  timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_la_dollar_ledger_user_created
                ON la_dollar_ledger (user_id, created_at_utc DESC);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_la_dollar_ledger_source
                ON la_dollar_ledger (user_id, reason, source_id)
                WHERE source_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_la_dollar_ledger_daily_attendance
                ON la_dollar_ledger (user_id, ((created_at_utc AT TIME ZONE 'UTC')::date))
                WHERE reason = 'attendance';

            CREATE TABLE IF NOT EXISTS la_dollar_store_items (
                id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                type             text NOT NULL,
                title            text NOT NULL,
                description      text,
                image_url        text,
                cost_la_dollars  integer NOT NULL,
                stock            integer,
                is_active        boolean NOT NULL DEFAULT true,
                created_at       timestamptz NOT NULL DEFAULT now(),
                updated_at       timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS la_dollar_purchases (
                id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id       uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                item_id       uuid NOT NULL REFERENCES la_dollar_store_items(id) ON DELETE RESTRICT,
                cost          integer NOT NULL,
                status        text NOT NULL DEFAULT 'pending'
                                 CHECK (status IN ('pending','fulfilled','cancelled')),
                created_at    timestamptz NOT NULL DEFAULT now(),
                fulfilled_at  timestamptz
            );
            CREATE INDEX IF NOT EXISTS ix_la_dollar_purchases_user_created
                ON la_dollar_purchases (user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_la_dollar_purchases_status
                ON la_dollar_purchases (status);

            -- ===== Phase 3: Speaking submissions =====
            CREATE TABLE IF NOT EXISTS speaking_submissions (
                id                       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id                  uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                content_id               uuid NOT NULL REFERENCES lesson_content(id) ON DELETE CASCADE,
                audio_url                text NOT NULL,
                transcript               text,
                ai_grade_pronunciation   integer,
                ai_grade_fluency         integer,
                ai_grade_grammar         integer,
                ai_grade_vocabulary      integer,
                ai_total                 integer,
                ai_feedback              text,
                ai_graded_at             timestamptz,
                teacher_id               uuid REFERENCES profiles(id) ON DELETE SET NULL,
                teacher_grade_total      integer,
                teacher_feedback         text,
                teacher_reviewed_at      timestamptz,
                final_grade              integer,
                status                   text NOT NULL DEFAULT 'submitted'
                                            CHECK (status IN ('submitted','ai_graded','teacher_reviewed')),
                created_at               timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_speaking_submissions_user_created
                ON speaking_submissions (user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_speaking_submissions_status
                ON speaking_submissions (status);

            -- ===== Phase 7: Writing submissions =====
            CREATE TABLE IF NOT EXISTS writing_submissions (
                id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id                     uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                content_id                  uuid NOT NULL REFERENCES lesson_content(id) ON DELETE CASCADE,
                text                        text NOT NULL,
                word_count                  integer NOT NULL DEFAULT 0,
                ai_grade_task_achievement   integer,
                ai_grade_coherence          integer,
                ai_grade_grammar            integer,
                ai_grade_vocabulary         integer,
                ai_total                    integer,
                ai_feedback                 text,
                ai_graded_at                timestamptz,
                teacher_id                  uuid REFERENCES profiles(id) ON DELETE SET NULL,
                teacher_grade_total         integer,
                teacher_feedback            text,
                teacher_reviewed_at         timestamptz,
                final_grade                 integer,
                status                      text NOT NULL DEFAULT 'submitted'
                                                CHECK (status IN ('submitted','ai_graded','teacher_reviewed')),
                created_at                  timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_writing_submissions_user_created
                ON writing_submissions (user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_writing_submissions_status
                ON writing_submissions (status);

            -- ===== Phase 16: Messaging =====
            CREATE TABLE IF NOT EXISTS messages (
                id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                sender_id     uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                recipient_id  uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                body          text NOT NULL,
                created_at    timestamptz NOT NULL DEFAULT now(),
                read_at       timestamptz
            );
            CREATE INDEX IF NOT EXISTS ix_messages_thread
                ON messages (sender_id, recipient_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_messages_unread
                ON messages (recipient_id) WHERE read_at IS NULL;

            -- ===== Phase 18: CRM / Lead Funnel =====
            CREATE TABLE IF NOT EXISTS leads (
                id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                full_name             text NOT NULL,
                phone                 text,
                email                 text,
                source                text NOT NULL DEFAULT 'other'
                                          CHECK (source IN ('walk_in','instagram','referral','website','ad','telegram','other')),
                stage                 text NOT NULL DEFAULT 'new'
                                          CHECK (stage IN ('new','contacted','trial_scheduled','converted','lost')),
                interest_level        text,
                notes                 text,
                assigned_to           uuid REFERENCES profiles(id) ON DELETE SET NULL,
                converted_profile_id  uuid REFERENCES profiles(id) ON DELETE SET NULL,
                trial_at              timestamptz,
                next_follow_up_at     timestamptz,
                lost_reason           text,
                created_at            timestamptz NOT NULL DEFAULT now(),
                updated_at            timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_leads_stage ON leads (stage);
            CREATE INDEX IF NOT EXISTS ix_leads_assigned ON leads (assigned_to);
            CREATE INDEX IF NOT EXISTS ix_leads_follow_up ON leads (next_follow_up_at);

            CREATE TABLE IF NOT EXISTS lead_activities (
                id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                lead_id     uuid NOT NULL REFERENCES leads(id) ON DELETE CASCADE,
                actor_id    uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                kind        text NOT NULL DEFAULT 'note'
                                CHECK (kind IN ('note','call','message','trial','stage_change','follow_up')),
                body        text NOT NULL,
                created_at  timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_lead_activities_thread ON lead_activities (lead_id, created_at DESC);

            -- ===== Phase 19: Multibranch =====
            CREATE TABLE IF NOT EXISTS branches (
                id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                name                text NOT NULL,
                code                text NOT NULL UNIQUE,
                city                text,
                address             text,
                phone               text,
                manager_name        text,
                manager_profile_id  uuid REFERENCES profiles(id) ON DELETE SET NULL,
                is_active           boolean NOT NULL DEFAULT true,
                created_at          timestamptz NOT NULL DEFAULT now(),
                updated_at          timestamptz NOT NULL DEFAULT now()
            );

            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS branch_id uuid REFERENCES branches(id) ON DELETE SET NULL;
            CREATE INDEX IF NOT EXISTS ix_profiles_branch ON profiles(branch_id);
            ALTER TABLE groups ADD COLUMN IF NOT EXISTS branch_id uuid REFERENCES branches(id) ON DELETE SET NULL;
            CREATE INDEX IF NOT EXISTS ix_groups_branch ON groups(branch_id);

            -- ===== Phase 20: Extra-curricular events =====
            CREATE TABLE IF NOT EXISTS club_events (
                id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                title              text NOT NULL,
                kind               text NOT NULL DEFAULT 'club'
                                       CHECK (kind IN ('club','workshop','debate','visit','social')),
                description        text,
                location           text,
                starts_at          timestamptz NOT NULL,
                ends_at            timestamptz,
                capacity           integer,
                la_dollar_reward   integer NOT NULL DEFAULT 0,
                created_by         uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                branch_id          uuid REFERENCES branches(id) ON DELETE SET NULL,
                is_cancelled       boolean NOT NULL DEFAULT false,
                cover_image_url    text,
                created_at         timestamptz NOT NULL DEFAULT now(),
                updated_at         timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_club_events_starts ON club_events (kind, starts_at);
            CREATE INDEX IF NOT EXISTS ix_club_events_branch ON club_events (branch_id);

            CREATE TABLE IF NOT EXISTS club_event_rsvps (
                id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                event_id    uuid NOT NULL REFERENCES club_events(id) ON DELETE CASCADE,
                user_id     uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                status      text NOT NULL DEFAULT 'going'
                                CHECK (status IN ('going','maybe','attended','no_show')),
                created_at  timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_club_event_rsvps_user_event
                ON club_event_rsvps (event_id, user_id);
            CREATE INDEX IF NOT EXISTS ix_club_event_rsvps_user ON club_event_rsvps (user_id);

            -- ===== Phase 21: Referrals + Alumni =====
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS referral_code text;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_profiles_referral_code
                ON profiles (referral_code) WHERE referral_code IS NOT NULL;
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS is_alumni boolean NOT NULL DEFAULT false;
            ALTER TABLE profiles ADD COLUMN IF NOT EXISTS alumni_note text;
            CREATE INDEX IF NOT EXISTS ix_profiles_alumni ON profiles (is_alumni) WHERE is_alumni;

            CREATE TABLE IF NOT EXISTS referrals (
                id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                referrer_id           uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                referee_profile_id    uuid REFERENCES profiles(id) ON DELETE SET NULL,
                lead_id               uuid REFERENCES leads(id) ON DELETE SET NULL,
                status                text NOT NULL DEFAULT 'pending'
                                          CHECK (status IN ('pending','converted','rewarded','expired')),
                reward_la_dollars     integer NOT NULL DEFAULT 0,
                created_at            timestamptz NOT NULL DEFAULT now(),
                converted_at          timestamptz,
                rewarded_at           timestamptz
            );
            CREATE INDEX IF NOT EXISTS ix_referrals_referrer ON referrals (referrer_id);
            CREATE INDEX IF NOT EXISTS ix_referrals_lead ON referrals (lead_id);

            -- ===== Phase 22: Legal / Compliance =====
            CREATE TABLE IF NOT EXISTS legal_documents (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                kind            text NOT NULL DEFAULT 'public_offer'
                                    CHECK (kind IN ('public_offer','terms','privacy','refund')),
                version         integer NOT NULL,
                title           text NOT NULL,
                body_markdown   text NOT NULL,
                is_current      boolean NOT NULL DEFAULT true,
                created_by      uuid REFERENCES profiles(id) ON DELETE SET NULL,
                effective_from  timestamptz NOT NULL DEFAULT now(),
                created_at      timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_legal_documents_kind_version
                ON legal_documents (kind, version);
            CREATE INDEX IF NOT EXISTS ix_legal_documents_current
                ON legal_documents (kind) WHERE is_current;

            CREATE TABLE IF NOT EXISTS legal_acceptances (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id         uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                document_id     uuid NOT NULL REFERENCES legal_documents(id) ON DELETE CASCADE,
                kind            text NOT NULL,
                version         integer NOT NULL,
                content_hash    text NOT NULL,
                user_agent      text,
                ip_address      text,
                accepted_at     timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_legal_acceptances_user_doc
                ON legal_acceptances (user_id, document_id);
            CREATE INDEX IF NOT EXISTS ix_legal_acceptances_user_kind
                ON legal_acceptances (user_id, kind);

            -- ===== Wisdom Lug'ati import history =====
            CREATE TABLE IF NOT EXISTS wisdom_import_jobs (
                id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                started_at           timestamptz NOT NULL DEFAULT now(),
                completed_at         timestamptz,
                state                text NOT NULL DEFAULT 'running'
                                         CHECK (state IN ('running','completed','failed','cancelled')),
                error                text,
                letters              text NOT NULL,
                current_letter       text,
                current_page         integer NOT NULL DEFAULT 0,
                last_page            integer NOT NULL DEFAULT 0,
                letters_done         integer NOT NULL DEFAULT 0,
                inserted             integer NOT NULL DEFAULT 0,
                updated              integer NOT NULL DEFAULT 0,
                unchanged_existing   integer NOT NULL DEFAULT 0,
                skipped              integer NOT NULL DEFAULT 0,
                failed_fetches       integer NOT NULL DEFAULT 0,
                min_star             integer NOT NULL DEFAULT 0,
                owner_id             uuid NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_wisdom_import_jobs_started
                ON wisdom_import_jobs (started_at DESC);
            -- On startup, mark any job left in 'running' state as cancelled
            -- (the process that owned it is gone).
            UPDATE wisdom_import_jobs SET state = 'cancelled', completed_at = now(),
                                          error = COALESCE(error, 'Backend restarted while running')
                WHERE state = 'running';

            -- ===== Phase 14: Parent portal =====
            DO $$
            DECLARE
                v_name text;
            BEGIN
                FOR v_name IN
                    SELECT constraint_name
                    FROM information_schema.table_constraints
                    WHERE table_name = 'profiles'
                      AND constraint_type = 'CHECK'
                      AND constraint_name LIKE '%role%'
                      AND constraint_name != 'profiles_role_check_v2'
                LOOP
                    EXECUTE 'ALTER TABLE profiles DROP CONSTRAINT ' || quote_ident(v_name);
                END LOOP;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE table_name = 'profiles' AND constraint_name = 'profiles_role_check_v2'
                ) THEN
                    ALTER TABLE profiles ADD CONSTRAINT profiles_role_check_v2 CHECK (role IN (
                        'admin', 'teacher', 'student', 'parent'
                    ));
                END IF;
            END$$;

            CREATE TABLE IF NOT EXISTS parent_child_links (
                id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                parent_id     uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                child_id      uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                relationship  text,
                created_at    timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_parent_child_link
                ON parent_child_links (parent_id, child_id);

            -- ===== Phase 12: Exams =====
            CREATE TABLE IF NOT EXISTS exams (
                id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                course_id            uuid REFERENCES courses(id) ON DELETE CASCADE,
                kind                 text NOT NULL,
                title                text NOT NULL,
                description          text,
                payload              jsonb NOT NULL,
                passing_score        integer NOT NULL DEFAULT 70,
                time_limit_seconds   integer,
                is_active            boolean NOT NULL DEFAULT true,
                created_at           timestamptz NOT NULL DEFAULT now(),
                updated_at           timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_exams_kind ON exams (kind);
            CREATE INDEX IF NOT EXISTS ix_exams_course ON exams (course_id);

            CREATE TABLE IF NOT EXISTS exam_attempts (
                id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id         uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                exam_id         uuid NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
                score           integer NOT NULL DEFAULT 0,
                out_of          integer NOT NULL DEFAULT 0,
                passed          boolean NOT NULL DEFAULT false,
                section_scores  jsonb,
                answers         jsonb,
                started_at      timestamptz NOT NULL DEFAULT now(),
                completed_at    timestamptz
            );
            CREATE INDEX IF NOT EXISTS ix_exam_attempts_user_exam
                ON exam_attempts (user_id, exam_id);

            -- ===== Phase 11: Achievements =====
            CREATE TABLE IF NOT EXISTS achievements (
                id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id       uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                category      text NOT NULL,
                period_year   integer NOT NULL,
                period_month  integer NOT NULL CHECK (period_month BETWEEN 1 AND 12),
                score         integer NOT NULL DEFAULT 0,
                awarded_at    timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_achievement_category_period
                ON achievements (category, period_year, period_month);
            CREATE INDEX IF NOT EXISTS ix_achievements_user
                ON achievements (user_id);

            -- ===== Phase 8: Teacher notes =====
            CREATE TABLE IF NOT EXISTS teacher_notes (
                id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                student_id   uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                author_id    uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
                kind         text NOT NULL DEFAULT 'observation'
                                CHECK (kind IN ('observation','praise','warning','plan')),
                body         text NOT NULL,
                created_at   timestamptz NOT NULL DEFAULT now(),
                updated_at   timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_teacher_notes_student_created
                ON teacher_notes (student_id, created_at DESC);

            -- ===== Lesson review workflow (admin verifies LLM-seeded answers) =====
            ALTER TABLE lessons ADD COLUMN IF NOT EXISTS is_reviewed boolean NOT NULL DEFAULT false;
            ALTER TABLE lessons ADD COLUMN IF NOT EXISTS reviewed_at_utc timestamptz;
            ALTER TABLE lessons ADD COLUMN IF NOT EXISTS reviewed_by uuid;
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE table_name = 'lessons' AND constraint_name = 'lessons_reviewed_by_fkey'
                ) THEN
                    ALTER TABLE lessons
                        ADD CONSTRAINT lessons_reviewed_by_fkey
                        FOREIGN KEY (reviewed_by) REFERENCES profiles(id) ON DELETE SET NULL;
                END IF;
            END$$;
            CREATE INDEX IF NOT EXISTS ix_lessons_is_reviewed ON lessons (is_reviewed);

            -- ===== Daily vocabulary system (image/frequency/topic + day grouping) =====
            ALTER TABLE words ADD COLUMN IF NOT EXISTS image_url       text;
            ALTER TABLE words ADD COLUMN IF NOT EXISTS frequency_rank  integer;
            ALTER TABLE words ADD COLUMN IF NOT EXISTS topic_tags      text[];
            CREATE INDEX IF NOT EXISTS ix_words_frequency_rank
                ON words (frequency_rank) WHERE frequency_rank IS NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_words_topic_tags
                ON words USING GIN (topic_tags);

            ALTER TABLE user_wordlist_entries
                ADD COLUMN IF NOT EXISTS day_number    integer;
            ALTER TABLE user_wordlist_entries
                ADD COLUMN IF NOT EXISTS source        text NOT NULL DEFAULT 'manual';
            ALTER TABLE user_wordlist_entries
                ADD COLUMN IF NOT EXISTS assigned_date date;
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE table_name = 'user_wordlist_entries'
                      AND constraint_name = 'user_wordlist_entries_source_check'
                ) THEN
                    ALTER TABLE user_wordlist_entries
                        ADD CONSTRAINT user_wordlist_entries_source_check
                        CHECK (source IN ('manual','system_auto','lesson_picked'));
                END IF;
            END$$;
            CREATE INDEX IF NOT EXISTS ix_user_wordlist_entries_user_day
                ON user_wordlist_entries (user_id, day_number)
                WHERE day_number IS NOT NULL;

            ALTER TABLE profiles
                ADD COLUMN IF NOT EXISTS vocab_target_level text NOT NULL DEFAULT 'A1';

            -- ===== Finance / Buxgalteriya =====
            CREATE TABLE IF NOT EXISTS finance_categories (
                id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                name        text NOT NULL,
                kind        text NOT NULL CHECK (kind IN ('income','expense')),
                color       text,
                is_active   boolean NOT NULL DEFAULT true,
                created_at  timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_finance_categories_kind_active
                ON finance_categories (kind, is_active);

            CREATE TABLE IF NOT EXISTS finance_transactions (
                id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                occurred_on  date NOT NULL,
                amount       numeric(14,2) NOT NULL CHECK (amount >= 0),
                kind         text NOT NULL CHECK (kind IN ('income','expense')),
                category_id  uuid REFERENCES finance_categories(id) ON DELETE SET NULL,
                description  text,
                created_by   uuid REFERENCES profiles(id) ON DELETE SET NULL,
                created_at   timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_finance_tx_occurred
                ON finance_transactions (occurred_on DESC);
            CREATE INDEX IF NOT EXISTS ix_finance_tx_kind_occurred
                ON finance_transactions (kind, occurred_on DESC);
            CREATE INDEX IF NOT EXISTS ix_finance_tx_category
                ON finance_transactions (category_id);

            -- Default categories (idempotent inserts) — admins can rename or delete.
            INSERT INTO finance_categories (name, kind, color)
            SELECT * FROM (VALUES
                ('Tuition', 'income', '#16a34a'),
                ('Donations', 'income', '#22c55e'),
                ('Other income', 'income', '#65a30d'),
                ('Salaries', 'expense', '#dc2626'),
                ('Utilities', 'expense', '#ea580c'),
                ('Rent', 'expense', '#f97316'),
                ('Supplies', 'expense', '#b45309'),
                ('Other expense', 'expense', '#a855f7')
            ) AS seed(name, kind, color)
            WHERE NOT EXISTS (
                SELECT 1 FROM finance_categories
                WHERE finance_categories.name = seed.name
                  AND finance_categories.kind = seed.kind
            );
        """);
    }

    private static async Task SeedAsync(AppDbContext db, SeedOptions seed, ILogger logger)
    {
        await EnsureSchemaUpgradesAsync(db);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(StudentPassword, workFactor: 10);
        var adminHash = BCrypt.Net.BCrypt.HashPassword(seed.AdminPassword, workFactor: 10);

        // ---- Profiles ----
        var admin = await db.Profiles.FirstOrDefaultAsync(p => p.Email == seed.AdminEmail);
        if (admin is null)
        {
            admin = new Profile
            {
                Email = seed.AdminEmail,
                PasswordHash = adminHash,
                FullName = "LangArt Admin",
                Role = Role.Admin,
                IsActive = true,
                EmailVerified = true,
            };
            db.Profiles.Add(admin);
            logger.LogInformation("Seeded admin {Email}", admin.Email);
        }
        else
        {
            // Refresh the admin's password to the configured DEFAULT_ADMIN_PASSWORD so the
            // login works after a clean schema bootstrap (where schema.sql may seed a
            // placeholder hash that BCrypt cannot verify).
            admin.PasswordHash = adminHash;
            admin.Role = Role.Admin;
            admin.IsActive = true;
            logger.LogInformation("Refreshed admin {Email} password", admin.Email);
        }

        var teacherSeeds = new[]
        {
            ("teacher.aiko@langartlms.com", "Aiko Tanaka"),
            ("teacher.mateo@langartlms.com", "Mateo Hernandez"),
        };
        var teachers = new List<Profile>();
        foreach (var (email, name) in teacherSeeds)
        {
            var existing = await db.Profiles.FirstOrDefaultAsync(p => p.Email == email);
            if (existing is null)
            {
                existing = new Profile
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FullName = name,
                    Role = Role.Teacher,
                    IsActive = true,
                    EmailVerified = true,
                };
                db.Profiles.Add(existing);
                logger.LogInformation("Seeded teacher {Email}", existing.Email);
            }
            teachers.Add(existing);
        }

        var studentSeeds = Enumerable.Range(1, 10)
            .Select(i => ($"student{i:00}@langartlms.com", $"Student {i:00}"))
            .ToList();
        var students = new List<Profile>();
        foreach (var (email, name) in studentSeeds)
        {
            var existing = await db.Profiles.FirstOrDefaultAsync(p => p.Email == email);
            if (existing is null)
            {
                existing = new Profile
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FullName = name,
                    Role = Role.Student,
                    IsActive = true,
                    EmailVerified = true,
                };
                db.Profiles.Add(existing);
            }
            students.Add(existing);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} students", students.Count);

        // ---- Courses ----
        var blueprints = BuildBlueprints();
        var courses = new List<Course>();
        foreach (var bp in blueprints)
        {
            var existing = await db.Courses.FirstOrDefaultAsync(c => c.Title == bp.Title);
            if (existing is null)
            {
                existing = new Course
                {
                    Title = bp.Title,
                    Description = bp.Description,
                    ThumbnailUrl = bp.ThumbnailUrl,
                    PriceMonthly = 19.99m,
                };
                db.Courses.Add(existing);
            }
            else if (string.IsNullOrEmpty(existing.ThumbnailUrl) && !string.IsNullOrEmpty(bp.ThumbnailUrl))
            {
                // Backfill thumbnail on existing courses created before the seed had one.
                existing.ThumbnailUrl = bp.ThumbnailUrl;
            }
            courses.Add(existing);
        }
        await db.SaveChangesAsync();

        // ---- Curriculum: modules → lessons → content per blueprint ----
        for (int i = 0; i < blueprints.Count; i++)
        {
            await SeedCurriculumAsync(db, courses[i], blueprints[i], logger);
        }

        // First course (English A1) and the new advanced courses get assigned to groups so
        // students immediately see varied content on the dashboard.
        var englishA1 = courses[0];
        var englishA2 = courses.First(c => c.Title.StartsWith("English A2", StringComparison.Ordinal));
        var french = courses.First(c => c.Title.StartsWith("French", StringComparison.Ordinal));
        var german = courses.First(c => c.Title.StartsWith("German", StringComparison.Ordinal));

        // ---- Classroom: one group per teacher with 5 students each ----
        for (int i = 0; i < teachers.Count; i++)
        {
            var teacher = teachers[i];
            var name = $"{teacher.FullName.Split(' ')[0]}'s Class A";
            var group = await db.Groups.FirstOrDefaultAsync(g => g.Name == name && g.TeacherId == teacher.Id);
            if (group is null)
            {
                group = new Group
                {
                    Name = name,
                    TeacherId = teacher.Id,
                    ScheduleInfo = "Mon/Wed 18:00-19:30",
                    ScheduleDays = new[] { "Mon", "Wed" },
                    StartTime = new TimeOnly(18, 0),
                    EndTime = new TimeOnly(19, 30),
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    IsActive = true,
                };
                db.Groups.Add(group);
                await db.SaveChangesAsync();

                var slice = students.Skip(i * 5).Take(5).ToList();
                foreach (var s in slice)
                {
                    db.GroupStudents.Add(new GroupStudent { GroupId = group.Id, StudentId = s.Id });
                }
                // Both groups get English A1, A2, and one other language so students see
                // the full progression (A1 → A2) plus a second language for variety.
                db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = englishA1.Id });
                db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = englishA2.Id });
                db.GroupCourses.Add(new GroupCourse
                {
                    GroupId = group.Id,
                    CourseId = i == 0 ? french.Id : german.Id,
                });
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded group {Name} with {N} students", group.Name, slice.Count);
            }
        }

        // ---- A couple of payments ----
        if (!await db.Payments.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var monthEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, DateTimeKind.Utc);
            foreach (var s in students.Take(3))
            {
                db.Payments.Add(new Payment
                {
                    UserId = s.Id,
                    CourseId = englishA1.Id,
                    Amount = 19.99m,
                    Currency = "USD",
                    Status = PaymentStatus.Completed,
                    PeriodStart = DateTime.SpecifyKind(now, DateTimeKind.Utc),
                    PeriodEnd = monthEnd,
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded sample payments");
        }

        // ---- Badges (Sprint 1 gamification) — idempotent, upserts by Code ----
        await SeedBadgesAsync(db, logger);

        // ---- LA Dollar store items (Phase 4) — idempotent, upserts by Title ----
        await SeedLaDollarStoreAsync(db, logger);
    }

    private static async Task SeedLaDollarStoreAsync(AppDbContext db, ILogger logger)
    {
        var catalog = new (string Type, string Title, string Description, int Cost, int? Stock)[]
        {
            ("coffee",       "Free coffee at the cafe",            "Redeem at the LangArt cafe counter.",                   50,  null),
            ("book",         "English novel of your choice",       "Pick from the small library shelf.",                   200,    20),
            ("discount",     "10% off next month's tuition",       "Applied automatically to your next invoice.",          500,   100),
            ("discount",     "25% off next month's tuition",       "Applied automatically to your next invoice.",         1200,    40),
            ("extra_lesson", "1 extra 1-on-1 lesson",              "Schedule with any available teacher.",                 800,    null),
            ("other",        "LangArt branded notebook",           "Pick up at the front desk.",                            100,    50),
        };

        var added = 0;
        foreach (var (type, title, description, cost, stock) in catalog)
        {
            var existing = await db.LaDollarStoreItems.FirstOrDefaultAsync(i => i.Title == title);
            if (existing is null)
            {
                db.LaDollarStoreItems.Add(new Data.Entities.LaDollarStoreItem
                {
                    Type = type,
                    Title = title,
                    Description = description,
                    CostLaDollars = cost,
                    Stock = stock,
                    IsActive = true,
                });
                added++;
            }
            else
            {
                existing.Type = type;
                existing.Description = description;
                existing.CostLaDollars = cost;
                if (existing.Stock is null && stock.HasValue) existing.Stock = stock;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} LA Dollar store item(s) ({Added} new)", catalog.Length, added);
    }

    private static async Task SeedBadgesAsync(AppDbContext db, ILogger logger)
    {
        var catalog = new[]
        {
            ("first_lesson",     "First Lesson",      "Complete your first lesson.",                            20),
            ("streak_7",         "Week-Long Learner", "Maintain a 7-day streak.",                              50),
            ("streak_30",        "Monthly Maven",     "Maintain a 30-day streak.",                            200),
            ("streak_100",       "Centurion",         "Maintain a 100-day streak.",                          1000),
            ("quiz_master_10",   "Quiz Apprentice",   "Pass 10 quizzes.",                                      75),
            ("quiz_master_50",   "Quiz Master",       "Pass 50 quizzes.",                                     300),
            ("quiz_perfect_10",  "Perfectionist",     "Score 100% on 10 quizzes.",                            150),
            ("polyglot",         "Polyglot",          "Make progress in two or more languages.",              100),
            ("early_bird",       "Early Bird",        "Complete a lesson before 8 AM (server UTC).",           30),
            ("night_owl",        "Night Owl",         "Complete a lesson after 11 PM (server UTC).",           30),
        };

        var added = 0;
        foreach (var (code, name, description, xp) in catalog)
        {
            var existing = await db.Badges.FirstOrDefaultAsync(b => b.Code == code);
            if (existing is null)
            {
                db.Badges.Add(new Data.Entities.Badge
                {
                    Code = code,
                    Name = name,
                    Description = description,
                    XpReward = xp,
                });
                added++;
            }
            else
            {
                // Keep description / name in sync with the catalog in case it was edited.
                existing.Name = name;
                existing.Description = description;
                existing.XpReward = xp;
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} badge(s) ({Added} new)", catalog.Length, added);
    }

    private static async Task SeedCurriculumAsync(AppDbContext db, Course course, CourseBlueprint bp, ILogger logger)
    {
        var moduleExists = await db.Modules.AnyAsync(m => m.CourseId == course.Id);
        if (moduleExists)
        {
            return; // idempotent — leave already-seeded curriculum alone
        }

        for (int mi = 0; mi < bp.Modules.Count; mi++)
        {
            var moduleBp = bp.Modules[mi];
            var module = new Data.Entities.Module
            {
                CourseId = course.Id,
                Title = moduleBp.Title,
                OrderIndex = mi,
            };
            db.Modules.Add(module);
            await db.SaveChangesAsync();

            for (int li = 0; li < moduleBp.Lessons.Count; li++)
            {
                var lessonBp = moduleBp.Lessons[li];
                var lesson = new Lesson
                {
                    ModuleId = module.Id,
                    Title = lessonBp.Title,
                    OrderIndex = li,
                };
                db.Lessons.Add(lesson);
                await db.SaveChangesAsync();

                for (int ci = 0; ci < lessonBp.Contents.Count; ci++)
                {
                    var contentBp = lessonBp.Contents[ci];
                    db.LessonContent.Add(new LessonContent
                    {
                        LessonId = lesson.Id,
                        Type = contentBp.Type,
                        ContentPayload = contentBp.Payload,
                        OrderIndex = ci,
                        ExerciseType = contentBp.ExerciseType,
                    });
                }
                await db.SaveChangesAsync();
            }
        }
        logger.LogInformation("Seeded curriculum for {Course}", course.Title);
    }

    private static async Task ClearAsync(AppDbContext db, ILogger logger)
    {
        // The live_sessions table is created lazily by EnsureSchemaUpgradesAsync, so
        // a clean DB without the upgrade applied yet would 404 here. Apply upgrades first.
        await EnsureSchemaUpgradesAsync(db);

        // Order matters because of FKs.
        await db.LiveSessions.ExecuteDeleteAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.StudentLessonAccess.ExecuteDeleteAsync();
        await db.QuizResults.ExecuteDeleteAsync();
        await db.LessonCompletions.ExecuteDeleteAsync();
        await db.Enrollments.ExecuteDeleteAsync();
        await db.Attendance.ExecuteDeleteAsync();
        await db.GroupCourses.ExecuteDeleteAsync();
        await db.GroupStudents.ExecuteDeleteAsync();
        await db.Groups.ExecuteDeleteAsync();
        await db.LessonResources.ExecuteDeleteAsync();
        await db.LessonContent.ExecuteDeleteAsync();
        await db.Lessons.ExecuteDeleteAsync();
        await db.Modules.ExecuteDeleteAsync();
        await db.Courses.ExecuteDeleteAsync();
        // Sprint 1 gamification — wipe user-specific state so reset is clean, but
        // keep the Badge catalog: SeedBadgesAsync below is idempotent.
        await db.PushSubscriptions.ExecuteDeleteAsync();
        await db.XpLedger.ExecuteDeleteAsync();
        await db.UserBadges.ExecuteDeleteAsync();
        await db.UserStreaks.ExecuteDeleteAsync();
        await db.UserXp.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        await db.Profiles.ExecuteDeleteAsync();
        logger.LogInformation("Cleared all seeded tables");
    }

    /// <summary>
    /// Deletes every Course whose title does NOT end with " Grammar", along with
    /// all dependent rows (modules, lessons, lesson_content, enrollments,
    /// group_courses, payments, etc.). Used when a project has accumulated demo
    /// courses and only the test-english Grammar courses should remain.
    /// Leaves profiles, groups, badges, and gamification state untouched.
    /// </summary>
    private static async Task ClearNonGrammarCoursesAsync(AppDbContext db, ILogger logger)
    {
        var victimIds = await db.Courses
            .Where(c => !c.Title.EndsWith(" Grammar"))
            .Select(c => c.Id)
            .ToListAsync();

        if (victimIds.Count == 0)
        {
            logger.LogInformation("ClearNonGrammar: nothing to delete (every course already ends with ' Grammar').");
            return;
        }
        logger.LogInformation("ClearNonGrammar: deleting {N} non-grammar course(s)…", victimIds.Count);

        var victimLessonIds = await db.Lessons
            .Where(l => victimIds.Contains(l.Module.CourseId))
            .Select(l => l.Id)
            .ToListAsync();

        var victimContentIds = await db.LessonContent
            .Where(lc => victimLessonIds.Contains(lc.LessonId))
            .Select(lc => lc.Id)
            .ToListAsync();

        await db.QuizResults
            .Where(qr => qr.ContentId.HasValue && victimContentIds.Contains(qr.ContentId.Value))
            .ExecuteDeleteAsync();
        // QuizResult also references the lesson directly, so any quiz that was
        // recorded against a lesson without a specific content row still cleans up.
        await db.QuizResults.Where(qr => victimLessonIds.Contains(qr.LessonId)).ExecuteDeleteAsync();
        await db.LessonCompletions.Where(lc => victimLessonIds.Contains(lc.LessonId)).ExecuteDeleteAsync();
        await db.StudentLessonAccess.Where(s => victimLessonIds.Contains(s.LessonId)).ExecuteDeleteAsync();
        await db.LessonResources.Where(r => victimLessonIds.Contains(r.LessonId)).ExecuteDeleteAsync();
        await db.LessonContent.Where(lc => victimLessonIds.Contains(lc.LessonId)).ExecuteDeleteAsync();
        await db.Lessons.Where(l => victimLessonIds.Contains(l.Id)).ExecuteDeleteAsync();
        await db.Modules.Where(m => victimIds.Contains(m.CourseId)).ExecuteDeleteAsync();
        await db.Enrollments.Where(e => victimIds.Contains(e.CourseId)).ExecuteDeleteAsync();
        await db.GroupCourses.Where(gc => victimIds.Contains(gc.CourseId)).ExecuteDeleteAsync();
        await db.Payments.Where(p => victimIds.Contains(p.CourseId)).ExecuteDeleteAsync();
        await db.Courses.Where(c => victimIds.Contains(c.Id)).ExecuteDeleteAsync();

        logger.LogInformation("ClearNonGrammar: removed {C} courses, {M} modules-worth, {L} lessons, {Lc} content blocks.",
            victimIds.Count, "—", victimLessonIds.Count, victimContentIds.Count);
    }

    // =================================================================================
    // Curriculum blueprints
    // =================================================================================

    private record CourseBlueprint(string Title, string Description, string ThumbnailUrl, IReadOnlyList<ModuleBp> Modules);
    private record ModuleBp(string Title, IReadOnlyList<LessonBp> Lessons);
    private record LessonBp(string Title, IReadOnlyList<ContentBp> Contents);
    private record ContentBp(ContentType Type, JsonDocument Payload, string? ExerciseType);
    private record Q(string Id, string Question, string[] Options, int Correct, string? Explanation = null);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonDocument ToDoc(object value) =>
        JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts));

    private static ContentBp Text(string title, string body) => new(
        ContentType.Text,
        ToDoc(new { Title = title, Body = body }),
        null);

    private static ContentBp Quiz(int passingScore, params Q[] questions) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "quiz",
            PassingScore = passingScore,
            Questions = questions.Select(q => new
            {
                Id = q.Id,
                Question = q.Question,
                Options = q.Options,
                CorrectAnswerIndex = q.Correct,
                Explanation = q.Explanation,
            }),
        }),
        "quiz");

    private static ContentBp Listening(string audioUrl, string audioTitle, int passingScore, params Q[] questions) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "listening",
            AudioUrl = audioUrl,
            AudioTitle = audioTitle,
            PassingScore = passingScore,
            Questions = questions.Select(q => new
            {
                Id = q.Id,
                Question = q.Question,
                Options = q.Options,
                CorrectAnswerIndex = q.Correct,
                Explanation = q.Explanation,
            }),
        }),
        "listening");

    private static ContentBp Writing(string prompt, int minWordCount) => new(
        ContentType.Exercise,
        ToDoc(new { ExerciseType = "writing", Prompt = prompt, MinWordCount = minWordCount }),
        "writing");

    private static ContentBp FillBlank(params string[] sentences) => new(
        ContentType.Exercise,
        ToDoc(new { ExerciseType = "fill_blank", Sentences = sentences }),
        "fill_blank");

    private static ContentBp Reading(string passageTitle, string passage, int passingScore, params Q[] questions) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "reading",
            PassageTitle = passageTitle,
            Passage = passage,
            PassingScore = passingScore,
            Questions = questions.Select(q => new
            {
                Id = q.Id,
                Question = q.Question,
                Options = q.Options,
                CorrectAnswerIndex = q.Correct,
                Explanation = q.Explanation,
            }),
        }),
        "reading");

    private static ContentBp Speaking(string promptType, string promptText, int maxDurationSeconds, string? imageUrl = null) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "speaking",
            PromptType = promptType,
            PromptText = promptText,
            ImageUrl = imageUrl,
            MaxDurationSeconds = maxDurationSeconds,
        }),
        "speaking");

    private static ContentBp VideoQuiz(
        string videoUrl,
        string videoTitle,
        string provider,
        string? intro,
        int passingScore,
        params Q[] questions) => new(
        ContentType.Exercise,
        ToDoc(new
        {
            ExerciseType = "video_quiz",
            VideoUrl = videoUrl,
            VideoTitle = videoTitle,
            VideoProvider = provider,
            Intro = intro,
            PassingScore = passingScore,
            LockUntilWatched = true,
            Questions = questions.Select(q => new
            {
                Id = q.Id,
                Question = q.Question,
                Options = q.Options,
                CorrectAnswerIndex = q.Correct,
                Explanation = q.Explanation,
            }),
        }),
        "video_quiz");

    /// <summary>
    /// Multilingual-aware grammar explanation. Renders as <see cref="GrammarExplanationContent"/>
    /// on the frontend (via the <c>kind: "grammar_explanation"</c> marker on the
    /// text payload), and supports per-language translations through the
    /// lesson_content_translations table.
    /// </summary>
    private static ContentBp GrammarExplanation(string title, string body) => new(
        ContentType.Text,
        ToDoc(new { Title = title, Body = body, Kind = "grammar_explanation" }),
        null);

    private static IReadOnlyList<CourseBlueprint> BuildBlueprints() => new[]
    {
        // -------------------------------------------------------------------- English A1
        new CourseBlueprint(
            "English A1 — Foundations",
            "Beginner-friendly path covering the essentials: greetings, present-tense verbs, everyday vocabulary.",
            "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=800&q=80",
            new[]
            {
                new ModuleBp("Getting Started", new[]
                {
                    new LessonBp("Saying Hello", new[]
                    {
                        GrammarExplanation("Welcome",
                            "# Hello!\n\nLet's start with basic greetings. **Hello**, *Hi*, and *Hey* are common in English.\n\n" +
                            "Use *Hello* in formal situations and *Hi* / *Hey* with friends."),
                        Quiz(70,
                            new Q("q1", "How do you say 'Hello' formally?", new[]{"Hi","Hello","Hey","Yo"}, 1),
                            new Q("q2", "Which is informal?", new[]{"Hello","Good morning","Hey","Greetings"}, 2)),
                        Reading("A Friendly Greeting",
                            "Sara walks into the office. She sees her colleague Tom at the coffee machine. \"Good morning, Tom!\" she says with a smile. Tom turns around. \"Hi Sara, how are you today?\" he replies. They chat for a few minutes about the weekend before heading to their desks.",
                            70,
                            new Q("rq1", "Where do they meet?", new[]{"At a restaurant","In the office","At home","On the street"}, 1),
                            new Q("rq2", "What do they talk about?", new[]{"Work projects","Their families","The weekend","Sports"}, 2)),
                        Speaking("topic_card",
                            "Introduce yourself in 30-60 seconds. Say your name, where you're from, and one thing you enjoy doing.",
                            60),
                    }),
                    new LessonBp("Numbers 1-10", new[]
                    {
                        GrammarExplanation("Counting",
                            "Practice the numbers one through ten: **one, two, three, four, five, six, seven, eight, nine, ten**."),
                        FillBlank(
                            "I have [two] eyes and [one] nose.",
                            "There are [seven] days in a week.",
                            "A spider has [eight] legs."),
                        Quiz(70,
                            new Q("nq1", "How many fingers do most people have?", new[]{"eight","ten","twelve","five"}, 1),
                            new Q("nq2", "Which number comes after 'three'?", new[]{"two","five","four","six"}, 2)),
                    }),
                }),
                new ModuleBp("Daily Conversations", new[]
                {
                    new LessonBp("Ordering Coffee", new[]
                    {
                        Text("At the Café", "Useful phrases: *Can I have...?*, *I'd like..., please.*, *How much is it?*"),
                        Speaking("topic_card",
                            "Order your favorite drink at a café. Start with a greeting, ask for the drink politely, and thank the barista.",
                            45),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- English A2
        new CourseBlueprint(
            "English A2 — Elementary",
            "Build on A1 basics: past tenses, daily routines, travel vocabulary, and short conversations.",
            "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?w=800&q=80",
            new[]
            {
                new ModuleBp("Talking About the Past", new[]
                {
                    new LessonBp("Past Simple — Regular Verbs", new[]
                    {
                        GrammarExplanation("Past Simple",
                            "# Past Simple Tense\n\nUse **past simple** for completed actions in the past.\n\n" +
                            "Regular verbs add **-ed**: *worked, played, visited*.\n\n" +
                            "- I **worked** late yesterday.\n" +
                            "- She **visited** her grandma last weekend.\n" +
                            "- They **played** football on Sunday.\n\n" +
                            "**Negative:** *did not (didn't) + base verb*\n" +
                            "**Question:** *Did + subject + base verb?*"),
                        VideoQuiz(
                            // Big Buck Bunny — Creative Commons, globally accessible demo video.
                            // Replace this with a real Past Simple grammar lesson via the admin Lesson Editor.
                            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4",
                            "📺 Demo Video — Replace with your own grammar lesson",
                            "raw",
                            "**⚠️ This is a demo video** showing how the Video+Quiz feature works. " +
                            "An admin can replace this with a real Past Simple grammar lesson via " +
                            "the Lesson Editor (`/admin/courses/.../lessons/...` → Edit this content). " +
                            "Common picks: BBC Learning English, engVid, or your own teacher-recorded YouTube video. " +
                            "The questions below test Past Simple regardless of which video plays here.",
                            70,
                            new Q("vq1", "Which verbs are regular in the past simple?",
                                new[]{"go, see, eat","work, play, watch","be, have, do","run, swim, write"}, 1,
                                "Regular verbs follow the -ed rule."),
                            new Q("vq2", "How do you form the past simple of regular verbs?",
                                new[]{"Add -s","Add -ing","Add -ed","Change the spelling completely"}, 2),
                            new Q("vq3", "Choose the correct negative form:",
                                new[]{"I didn't worked.","I didn't work.","I not worked.","I no work."}, 1,
                                "Use 'didn't + base verb' for negatives."),
                            new Q("vq4", "Choose the correct question form:",
                                new[]{"Did you played football?","Did you play football?","You did play football?","You played football?"}, 1)),
                        FillBlank(
                            "Yesterday I [walked] to the park.",
                            "She [played] tennis last Friday.",
                            "They [watched] a film together.",
                            "He [studied] hard for the exam."),
                        Quiz(70,
                            new Q("psq1", "What is the past simple of 'work'?", new[]{"worked","working","works","work"}, 0),
                            new Q("psq2", "Choose the correct sentence:",
                                new[]{"She play tennis yesterday.","She played tennis yesterday.","She is playing tennis yesterday.","She plays tennis yesterday."}, 1),
                            new Q("psq3", "Form the negative: 'He visited Paris.'",
                                new[]{"He no visited Paris.","He didn't visit Paris.","He didn't visited Paris.","He not visit Paris."}, 1)),
                        Reading("My Weekend",
                            "Last weekend was wonderful. On Saturday morning I walked in the park with my dog. We played with a ball for an hour. Then I visited my parents and we cooked lunch together. On Sunday I stayed home and watched two films. I really enjoyed my weekend.",
                            70,
                            new Q("psrq1", "What did the writer do on Saturday morning?",
                                new[]{"Cooked lunch","Walked in the park","Watched films","Visited parents"}, 1),
                            new Q("psrq2", "How many films did they watch on Sunday?",
                                new[]{"One","Two","Three","None"}, 1),
                            new Q("psrq3", "How did the writer feel about the weekend?",
                                new[]{"Bored","Tired","Wonderful","Disappointed"}, 2)),
                        Speaking("topic_card",
                            "Talk about your last weekend in 45 seconds. What did you do? Use at least 5 past-simple verbs.",
                            60),
                    }),
                    new LessonBp("Irregular Verbs", new[]
                    {
                        GrammarExplanation("Common Irregular Verbs",
                            "Some verbs don't follow the -ed rule. You need to memorize them:\n\n" +
                            "| Base | Past Simple |\n|---|---|\n| go | went |\n| have | had |\n| see | saw |\n| eat | ate |\n| make | made |\n| do | did |\n| say | said |\n| come | came |"),
                        Quiz(70,
                            new Q("ivq1", "What is the past simple of 'go'?", new[]{"goed","went","gone","goes"}, 1),
                            new Q("ivq2", "Past simple of 'have'?", new[]{"haved","has","had","having"}, 2),
                            new Q("ivq3", "Choose correct: I ___ pizza for dinner.", new[]{"eated","ate","eat","eaten"}, 1),
                            new Q("ivq4", "Past simple of 'see'?", new[]{"seed","saw","seen","sees"}, 1)),
                        FillBlank(
                            "I [went] to school yesterday.",
                            "She [had] a great time at the party.",
                            "We [ate] sushi for lunch.",
                            "They [saw] a film last night."),
                    }),
                }),
                new ModuleBp("Daily Routines", new[]
                {
                    new LessonBp("Describing Your Day", new[]
                    {
                        GrammarExplanation("Adverbs of Frequency",
                            "Use **adverbs of frequency** to say how often you do something.\n\n" +
                            "**always (100%)** → usually → often → sometimes → rarely → **never (0%)**\n\n" +
                            "They go **before** the main verb but **after** the verb *to be*:\n" +
                            "- I **always** eat breakfast.\n" +
                            "- She is **never** late."),
                        Reading("A Morning Routine",
                            "Maya is a teacher. She always wakes up at 6:30 a.m. and drinks a cup of coffee. She usually walks to school, but she sometimes takes the bus when it rains. She arrives at 7:45 and prepares her classroom. Her first lesson starts at 8:15. She never eats breakfast before school — she has a snack at 10.",
                            70,
                            new Q("drrq1", "What time does Maya wake up?",
                                new[]{"6:00","6:30","7:00","8:00"}, 1),
                            new Q("drrq2", "How does she usually get to school?",
                                new[]{"By car","By bus","On foot","By bike"}, 2),
                            new Q("drrq3", "When does she have breakfast?",
                                new[]{"At 6:30","Before school","She doesn't have breakfast","At 8:15"}, 2)),
                        Speaking("topic_card",
                            "Describe your typical weekday morning. Use at least 3 adverbs of frequency (always, usually, often, sometimes, never).",
                            60),
                        Writing(
                            "Write a short paragraph (50-100 words) describing your daily routine. Include what time you wake up, how often you eat breakfast, and how you get to work/school.",
                            50),
                    }),
                }),
                new ModuleBp("Travel & Places", new[]
                {
                    new LessonBp("At the Airport", new[]
                    {
                        Text("Useful Vocabulary",
                            "**Key terms:** boarding pass, check-in, gate, departure, arrival, luggage, passport, customs, security."),
                        Listening(
                            "https://download.samplelib.com/mp3/sample-15s.mp3",
                            "Airport Announcement (Sample)",
                            70,
                            new Q("apl1", "This is a placeholder audio. Once the teacher uploads a real airport announcement, the questions become meaningful. What kind of audio is intended here?",
                                new[]{"A song","An airport announcement","A weather report","A film soundtrack"}, 1,
                                "Replace the audio URL via the admin Lesson Editor with a real recording (e.g. uploaded via /api/uploads/audio)."),
                            new Q("apl2", "Where would you typically hear an airport announcement?",
                                new[]{"At a restaurant","At an airport","At home","In a library"}, 1)),
                        Quiz(70,
                            new Q("apq1", "What document do you show at check-in?",
                                new[]{"Receipt","Boarding pass","Passport","Map"}, 2),
                            new Q("apq2", "Where do you go after check-in?",
                                new[]{"Home","Security","Customs","Restaurant"}, 1),
                            new Q("apq3", "What is 'luggage'?",
                                new[]{"Food","Bags and suitcases","Tickets","A vehicle"}, 1)),
                        Reading("Maria's First Flight",
                            "Maria arrived at the airport two hours before her flight. She found the check-in counter and showed her passport to the agent. The agent gave her a boarding pass and a seat near the window. After check-in, Maria walked to security where she put her bag on the X-ray machine. Then she waited at gate 24 and read a magazine until boarding time.",
                            70,
                            new Q("aprq1", "When did Maria arrive at the airport?",
                                new[]{"One hour before","Two hours before","Just in time","Late"}, 1),
                            new Q("aprq2", "What kind of seat did she get?",
                                new[]{"Aisle","Window","Middle","Business class"}, 1),
                            new Q("aprq3", "What did she do at gate 24?",
                                new[]{"Bought food","Slept","Read a magazine","Called a friend"}, 2)),
                        Speaking("interactive",
                            "Imagine you're at the airport check-in counter. Greet the agent, ask about your seat, and confirm your luggage.",
                            45),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- English B1
        new CourseBlueprint(
            "English B1 — Intermediate",
            "Build fluency: tenses, conditionals, idiomatic expressions, and conversational practice.",
            "https://images.unsplash.com/photo-1457369804613-52c61a468e7d?w=800&q=80",
            new[]
            {
                new ModuleBp("Conditionals & Hypotheticals", new[]
                {
                    new LessonBp("Zero & First Conditional", new[]
                    {
                        Text("If-clauses",
                            "# Conditionals\n\n**Zero conditional** — general truths: *If you heat water to 100°C, it boils.*\n\n" +
                            "**First conditional** — real future possibilities: *If it rains tomorrow, we'll stay home.*\n\n" +
                            "Form: **If + present simple, ... will + base verb**."),
                        Quiz(75,
                            new Q("q1", "Pick the zero-conditional sentence:",
                                new[]{"If I have time, I will call you.","If you press this button, the light turns on.","If I were you, I'd accept the job.","If she had studied, she would have passed."}, 1,
                                "Zero conditional uses present tense in both clauses for general truths."),
                            new Q("q2", "If it ___ tomorrow, we'll cancel the picnic.",
                                new[]{"rains","will rain","rained","would rain"}, 0,
                                "First conditional uses present simple in the if-clause."),
                            new Q("q3", "Which form follows 'If + present simple, ...'?",
                                new[]{"would + base","will + base","had + past participle","were + base"}, 1),
                            new Q("q4", "Pick the first-conditional sentence:",
                                new[]{"If I win, I'll celebrate.","If I won, I'd celebrate.","If I had won, I'd have celebrated.","If I'm winning, I celebrate."}, 0)),
                    }),
                    new LessonBp("Second Conditional Writing", new[]
                    {
                        Text("Imagining the Unreal",
                            "Second conditional describes hypothetical situations: *If I had a million dollars, I would travel the world.*\n\n" +
                            "Form: **If + past simple, ... would + base verb.**"),
                        Writing(
                            "If you could live anywhere in the world, where would you live and why? Use the second conditional at least twice. (min 80 words)",
                            80),
                    }),
                }),
                new ModuleBp("Idiomatic English", new[]
                {
                    new LessonBp("Common Idioms", new[]
                    {
                        Text("Idioms",
                            "Idioms have meanings that go beyond their literal words. Examples:\n\n" +
                            "- **Break the ice** — start a conversation\n" +
                            "- **Hit the nail on the head** — describe something exactly\n" +
                            "- **Under the weather** — feeling sick\n" +
                            "- **Piece of cake** — very easy\n" +
                            "- **Cost an arm and a leg** — very expensive"),
                        Quiz(70,
                            new Q("q1", "She felt 'under the weather' — what does that mean?",
                                new[]{"She was outside in the rain","She was feeling unwell","She was traveling","She was upset"}, 1),
                            new Q("q2", "'A piece of cake' means:",
                                new[]{"A small dessert","Something very easy","Something delicious","Something expensive"}, 1),
                            new Q("q3", "'Break the ice' is used when you:",
                                new[]{"Cool down a drink","Start a conversation","End a friendship","Cancel a meeting"}, 1),
                            new Q("q4", "Something that 'costs an arm and a leg' is:",
                                new[]{"Free","Cheap","Very expensive","Painful"}, 2),
                            new Q("q5", "'Hit the nail on the head' means:",
                                new[]{"Be exactly right","Make a mistake","Work very hard","Be lucky"}, 0)),
                    }),
                    new LessonBp("Phrasal Verbs", new[]
                    {
                        GrammarExplanation("Phrasal Verbs",
                            "Phrasal verbs are verb + particle combinations: **give up**, **look after**, **run into**, **bring up**.\n\n" +
                            "They often have meanings that differ from the parts.\n\n" +
                            "- **give up** = stop trying\n" +
                            "- **look after** = take care of\n" +
                            "- **run into** = meet by chance\n" +
                            "- **bring up** = mention; raise a child"),
                        Reading("A Chance Meeting",
                            "Yesterday I ran into my old school friend Anna at the supermarket. We had not seen each other for ten years! She told me she gave up her job in marketing two years ago to look after her newborn son. Now she runs a small online bakery from home. We had coffee together and brought up so many memories from our school days.",
                            70,
                            new Q("pvrq1", "Where did the writer meet Anna?",
                                new[]{"At school","At a café","At the supermarket","At work"}, 2),
                            new Q("pvrq2", "Why did Anna give up her job?",
                                new[]{"She was bored","To look after her son","To travel","She was fired"}, 1),
                            new Q("pvrq3", "What does Anna do now?",
                                new[]{"Marketing","Teaching","Online bakery","She doesn't work"}, 2),
                            new Q("pvrq4", "What did they 'bring up' during coffee?",
                                new[]{"Children","Memories","Complaints","Jobs"}, 1)),
                        Writing(
                            "Write a short story (min 100 words) using at least four phrasal verbs. Underline each phrasal verb you use.",
                            100),
                    }),
                }),
                new ModuleBp("Speaking Practice", new[]
                {
                    new LessonBp("Describing a Photo", new[]
                    {
                        GrammarExplanation("Useful phrases for photo description",
                            "When describing a photo, use these structures:\n\n" +
                            "- **In the foreground / In the background** — what's near or far\n" +
                            "- **On the left / right / in the middle** — position\n" +
                            "- **It looks like / It seems that** — guessing\n" +
                            "- **There is / there are** — listing what you see"),
                        Speaking("photo_description",
                            "Describe what you see in your favorite recent photo. Talk about the people, place, and atmosphere. Use at least 3 location phrases (in the background, on the left, etc.).",
                            90),
                    }),
                    new LessonBp("Giving Your Opinion", new[]
                    {
                        GrammarExplanation("Expressing opinions",
                            "Phrases to share your view:\n\n" +
                            "- **In my opinion / I think / I believe**\n" +
                            "- **From my point of view**\n" +
                            "- **As far as I'm concerned**\n" +
                            "- **I'd argue that...**\n\n" +
                            "To agree: *I couldn't agree more / That's a good point.*\n" +
                            "To disagree politely: *I see what you mean, but... / I'm not sure about that.*"),
                        Speaking("topic_card",
                            "Should social media be limited for teenagers? Give your opinion in 60-90 seconds. Use at least 3 opinion phrases and 1 example.",
                            90),
                        Writing(
                            "Write a 150-word opinion paragraph on: 'Is remote work better than working in an office?' Give 2 reasons and 1 example.",
                            150),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- Spanish A1
        new CourseBlueprint(
            "Spanish A1 — Hola y Mucho Más",
            "Start speaking Spanish from day one with cultural notes and bite-sized lessons.",
            "https://images.unsplash.com/photo-1583422409516-2895a77efded?w=800&q=80",
            new[]
            {
                new ModuleBp("Saludos y Presentaciones", new[]
                {
                    new LessonBp("Hola y Adiós", new[]
                    {
                        Text("Saludos básicos",
                            "# Hola\n\nLos saludos más comunes:\n\n- **Hola** — Hello\n- **Buenos días** — Good morning\n- **Buenas tardes** — Good afternoon\n- **Buenas noches** — Good evening / night\n- **Adiós** — Goodbye\n- **Hasta luego** — See you later"),
                        Quiz(70,
                            new Q("q1", "How do you say 'Good morning' in Spanish?",
                                new[]{"Buenas noches","Buenas tardes","Buenos días","Hola"}, 2),
                            new Q("q2", "'Hasta luego' means:",
                                new[]{"Hello","See you later","Good night","Thank you"}, 1),
                            new Q("q3", "Pick the evening greeting:",
                                new[]{"Buenos días","Buenas tardes","Buenas noches","Adiós"}, 2),
                            new Q("q4", "'Adiós' translates to:",
                                new[]{"Hello","Yes","Goodbye","Please"}, 2)),
                    }),
                    new LessonBp("Presentándote", new[]
                    {
                        Text("Yo me llamo...",
                            "Para presentarte:\n\n- **Me llamo + name** — My name is...\n- **Yo soy de + place** — I am from...\n- **Tengo + age + años** — I am ... years old."),
                        FillBlank(
                            "Me [llamo] Ana.",
                            "Yo [soy] de España.",
                            "[Tengo] veinte años.",
                            "Mucho [gusto] en conocerte."),
                    }),
                }),
                new ModuleBp("Números y Tiempo", new[]
                {
                    new LessonBp("Los Números 1-20", new[]
                    {
                        Text("Contando",
                            "1 uno, 2 dos, 3 tres, 4 cuatro, 5 cinco, 6 seis, 7 siete, 8 ocho, 9 nueve, 10 diez, " +
                            "11 once, 12 doce, 13 trece, 14 catorce, 15 quince, 16 dieciséis, 17 diecisiete, 18 dieciocho, 19 diecinueve, 20 veinte."),
                        FillBlank(
                            "Tres más cuatro son [siete].",
                            "Diez menos dos son [ocho].",
                            "Cinco por dos son [diez]."),
                    }),
                    new LessonBp("¿Qué Hora Es?", new[]
                    {
                        Text("La Hora",
                            "**Es la una.** — It's one o'clock.\n**Son las dos.** — It's two o'clock.\n**Son las tres y media.** — It's half past three."),
                        Quiz(60,
                            new Q("q1", "How would you say 'It's one o'clock'?",
                                new[]{"Son la una","Es la una","Son las una","Es las una"}, 1),
                            new Q("q2", "'Son las tres y media' means:",
                                new[]{"3:00","3:15","3:30","3:45"}, 2),
                            new Q("q3", "Pick the correct form for 'It's five o'clock':",
                                new[]{"Es las cinco","Son las cinco","Es la cinco","Son la cinco"}, 1)),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- French B1
        new CourseBlueprint(
            "French B1 — Conversational Mastery",
            "Move beyond textbook French: real-world café conversations, travel scenarios, and listening practice.",
            "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800&q=80",
            new[]
            {
                new ModuleBp("Au Café", new[]
                {
                    new LessonBp("Commander une boisson", new[]
                    {
                        Text("À la terrasse",
                            "# Au Café\n\nLes formules polies pour commander :\n\n" +
                            "- *Bonjour, je voudrais un café, s'il vous plaît.*\n" +
                            "- *Pourriez-vous m'apporter l'addition ?*\n" +
                            "- *Est-ce que je peux avoir un verre d'eau ?*\n\n" +
                            "**Astuce :** En France, on dit toujours *bonjour* avant de commander."),
                        Quiz(75,
                            new Q("q1", "Comment commander poliment un café ?",
                                new[]{"Je veux un café","Donnez-moi un café","Je voudrais un café, s'il vous plaît","Café !"}, 2,
                                "La forme conditionnelle 'je voudrais' est plus polie que 'je veux'."),
                            new Q("q2", "Pour demander l'addition :",
                                new[]{"L'addition, s'il vous plaît","Le menu, s'il vous plaît","La carte, s'il vous plaît","L'eau, s'il vous plaît"}, 0),
                            new Q("q3", "Quel mot signifie 'tip' en français ?",
                                new[]{"L'addition","Le pourboire","La monnaie","La caisse"}, 1)),
                    }),
                    new LessonBp("Conversation au comptoir", new[]
                    {
                        Text("Écoutez bien",
                            "Dans cet exercice, écoutez la conversation à un comptoir français et répondez aux questions de compréhension."),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/9/96/Fr-bonjour.ogg",
                            "Bonjour — prononciation française",
                            60,
                            new Q("q1", "Quel salut entendez-vous au début ?",
                                new[]{"Bonjour","Bonsoir","Salut","Au revoir"}, 0),
                            new Q("q2", "Le 'r' dans 'bonjour' se prononce :",
                                new[]{"comme en anglais","de la gorge (uvulaire)","silencieux","comme un 'l'"}, 1),
                            new Q("q3", "On utilise 'bonjour' :",
                                new[]{"seulement le matin","toute la journée","seulement le soir","la nuit"}, 1)),
                    }),
                }),
                new ModuleBp("Voyages", new[]
                {
                    new LessonBp("À la gare", new[]
                    {
                        Text("Prendre le train",
                            "Phrases utiles à la gare :\n\n- *Un aller-retour pour Paris, s'il vous plaît.*\n" +
                            "- *Le prochain train part à quelle heure ?*\n- *Sur quel quai ?*"),
                        Quiz(70,
                            new Q("q1", "'Un aller-retour' veut dire :",
                                new[]{"One-way ticket","Round-trip ticket","First-class ticket","Reservation"}, 1),
                            new Q("q2", "Vous demandez le quai du train :",
                                new[]{"Quel est le prix ?","Sur quel quai ?","Quand arrive-t-il ?","Combien de temps ?"}, 1),
                            new Q("q3", "'Le prochain train' signifie :",
                                new[]{"The previous train","The first train","The next train","The last train"}, 2)),
                    }),
                    new LessonBp("Demander son chemin", new[]
                    {
                        Text("Excusez-moi…",
                            "Pour demander son chemin :\n\n- *Excusez-moi, où se trouve la gare ?*\n- *C'est loin d'ici ?*\n- *Tournez à droite, puis tout droit.*"),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/9/96/Fr-bonjour.ogg",
                            "Demander son chemin (extrait)",
                            60,
                            new Q("q1", "Comment commencer poliment ?",
                                new[]{"Hé !","Excusez-moi","Toi, là !","Bonsoir"}, 1),
                            new Q("q2", "'Tournez à droite' signifie :",
                                new[]{"Turn left","Turn right","Go straight","Stop"}, 1)),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- German B1
        new CourseBlueprint(
            "German B1 — Building Fluency",
            "Master separable verbs, the Konjunktiv II, and write confidently about daily life and hypotheticals.",
            "https://images.unsplash.com/photo-1467269204594-9661b134dd2b?w=800&q=80",
            new[]
            {
                new ModuleBp("Trennbare Verben", new[]
                {
                    new LessonBp("Anfangen, aufstehen, ausgehen", new[]
                    {
                        Text("Was sind trennbare Verben?",
                            "# Trennbare Verben\n\nManche deutsche Verben haben ein Präfix, das sich vom Stamm trennt:\n\n" +
                            "- **aufstehen** — Ich **stehe** um 7 Uhr **auf**.\n" +
                            "- **anfangen** — Der Film **fängt** um 20 Uhr **an**.\n" +
                            "- **ausgehen** — Wir **gehen** heute Abend **aus**.\n" +
                            "- **einkaufen** — Sie **kauft** im Supermarkt **ein**."),
                        FillBlank(
                            "Ich [stehe] um 7 Uhr [auf].",
                            "Der Film [fängt] um 20 Uhr [an].",
                            "Wir [gehen] heute Abend [aus].",
                            "Sie [kauft] gerne [ein]."),
                    }),
                    new LessonBp("Schreibübung: Mein Tag", new[]
                    {
                        Text("Tagesablauf",
                            "Schreibe über deinen typischen Tag. Benutze mindestens drei trennbare Verben (z. B. *aufstehen*, *anrufen*, *einkaufen*, *ausgehen*)."),
                        Writing(
                            "Beschreibe deinen typischen Tag von morgens bis abends. Verwende mindestens drei trennbare Verben. (mindestens 60 Wörter)",
                            60),
                    }),
                }),
                new ModuleBp("Konjunktiv II", new[]
                {
                    new LessonBp("Wenn ich…", new[]
                    {
                        Text("Hypothesen ausdrücken",
                            "Der Konjunktiv II drückt Hypothesen oder Wünsche aus:\n\n" +
                            "- *Wenn ich Zeit **hätte**, **würde** ich mehr lesen.*\n" +
                            "- *Ich **wäre** gerne in Berlin.*\n\n" +
                            "Hilfsverben im Konjunktiv II: **hätte**, **wäre**, **würde + Infinitiv**."),
                        FillBlank(
                            "Wenn ich Zeit [hätte], [würde] ich mehr lesen.",
                            "Ich [wäre] gerne in Berlin.",
                            "Wenn er hier [wäre], [würde] er helfen."),
                    }),
                    new LessonBp("Höfliche Bitten", new[]
                    {
                        Text("Höflichkeit",
                            "Mit dem Konjunktiv II kannst du höflich bitten:\n\n" +
                            "- *Könnten Sie mir helfen?*\n- *Hätten Sie einen Moment?*\n- *Würden Sie das wiederholen, bitte?*"),
                        Writing(
                            "Stell dir vor, du sprichst mit einem neuen Kollegen. Schreibe einen kurzen Dialog (mindestens 80 Wörter) mit mindestens drei höflichen Bitten im Konjunktiv II.",
                            80),
                    }),
                }),
            }),

        // -------------------------------------------------------------------- Italian B1
        new CourseBlueprint(
            "Italian B1 — Caffè e Cultura",
            "Sip, listen, and converse: B1-level Italian rooted in everyday café and kitchen scenes.",
            "https://images.unsplash.com/photo-1525610553991-2bede1a236e2?w=800&q=80",
            new[]
            {
                new ModuleBp("Al Bar", new[]
                {
                    new LessonBp("Ordinare un caffè", new[]
                    {
                        Text("Al banco",
                            "# Al Bar\n\nIn Italia il *bar* è dove si beve il caffè in piedi.\n\n" +
                            "- *Un caffè, per favore.* — un espresso\n" +
                            "- *Un cappuccino e un cornetto.* — colazione tipica\n" +
                            "- *Un caffè macchiato.* — espresso con un goccio di latte\n\n" +
                            "**Curiosità:** gli italiani non bevono cappuccino dopo pranzo!"),
                        Quiz(70,
                            new Q("q1", "Cosa significa 'un caffè' in Italia?",
                                new[]{"Un americano","Un espresso","Un cappuccino","Un latte"}, 1,
                                "In Italia 'un caffè' è sempre un espresso."),
                            new Q("q2", "Quando si beve il cappuccino?",
                                new[]{"Al mattino","A pranzo","Dopo cena","A tutte le ore"}, 0),
                            new Q("q3", "'Un caffè macchiato' è :",
                                new[]{"Un caffè con molto latte","Un espresso con un goccio di latte","Un caffè freddo","Un caffè senza zucchero"}, 1)),
                    }),
                    new LessonBp("Conversazione informale", new[]
                    {
                        Text("Ascolta",
                            "Ascolta questa breve conversazione al bar e rispondi alle domande di comprensione."),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/6/64/It-buongiorno.ogg",
                            "Buongiorno — pronuncia italiana",
                            60,
                            new Q("q1", "Quale saluto senti?",
                                new[]{"Buongiorno","Buonasera","Ciao","Salve"}, 0),
                            new Q("q2", "'Buongiorno' si usa :",
                                new[]{"solo la mattina presto","dalla mattina fino al primo pomeriggio","solo la sera","di notte"}, 1),
                            new Q("q3", "Una versione più informale di 'Buongiorno' è :",
                                new[]{"Salve","Buonasera","Ciao","Buonanotte"}, 2)),
                    }),
                }),
                new ModuleBp("In Cucina", new[]
                {
                    new LessonBp("Ingredienti italiani", new[]
                    {
                        Text("Sapori autentici",
                            "Gli ingredienti chiave della cucina italiana:\n\n" +
                            "- **olio d'oliva** — olive oil\n- **pomodoro** — tomato\n- **basilico** — basil\n" +
                            "- **parmigiano** — Parmesan cheese\n- **pasta fresca** — fresh pasta"),
                        Quiz(70,
                            new Q("q1", "'Olio d'oliva' è :",
                                new[]{"Vinegar","Olive oil","Butter","Sunflower oil"}, 1),
                            new Q("q2", "Quale erba è essenziale nel pesto?",
                                new[]{"Prezzemolo","Origano","Basilico","Rosmarino"}, 2),
                            new Q("q3", "'Pasta fresca' significa :",
                                new[]{"Dry pasta","Fresh pasta","Cold pasta","Pasta sauce"}, 1)),
                    }),
                    new LessonBp("Ricette tradizionali", new[]
                    {
                        Text("Pasta al pomodoro",
                            "Una ricetta classica in pochi passi: fai bollire l'acqua, aggiungi il sale, cuoci la pasta *al dente*, poi unisci una salsa di pomodoro, basilico fresco e parmigiano grattugiato."),
                        Listening(
                            "https://upload.wikimedia.org/wikipedia/commons/6/64/It-buongiorno.ogg",
                            "Pronuncia: 'al dente'",
                            60,
                            new Q("q1", "'Al dente' descrive :",
                                new[]{"Pasta overcooked","Pasta firm to the bite","Pasta cold","Pasta with sauce"}, 1),
                            new Q("q2", "Il basilico è :",
                                new[]{"Una spezia secca","Un'erba fresca aromatica","Una salsa","Un formaggio"}, 1)),
                    }),
                }),
            }),
    };
}
