-- LangArt Self-Hosted Database Schema
-- PostgreSQL 15+
-- No external dependencies (Supabase Auth removed)

-- ============================================
-- ENUMS
-- ============================================

CREATE TYPE attendance_status AS ENUM ('present', 'absent', 'late', 'excused');
CREATE TYPE content_type AS ENUM ('text', 'video', 'audio', 'slide', 'exercise');

-- ============================================
-- CORE TABLES
-- ============================================

-- Users/Profiles (self-contained with auth)
CREATE TABLE public.profiles (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT UNIQUE NOT NULL,
  password_hash TEXT NOT NULL,
  full_name TEXT NOT NULL DEFAULT '',
  role TEXT NOT NULL DEFAULT 'student' CHECK (role = ANY (ARRAY['admin', 'teacher', 'student'])),
  is_active BOOLEAN NOT NULL DEFAULT true,
  email_verified BOOLEAN DEFAULT false,
  last_login TIMESTAMP WITH TIME ZONE,
  reset_token TEXT,
  reset_token_expires TIMESTAMP WITH TIME ZONE,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Sessions for JWT refresh tokens
CREATE TABLE public.sessions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  refresh_token TEXT NOT NULL UNIQUE,
  expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
  last_used_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
  user_agent TEXT,
  ip_address INET
);

-- ============================================
-- CURRICULUM TABLES
-- ============================================

-- Courses
CREATE TABLE public.courses (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  title TEXT NOT NULL,
  description TEXT,
  thumbnail_url TEXT,
  price_monthly NUMERIC,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Modules (course sections)
CREATE TABLE public.modules (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  course_id UUID NOT NULL REFERENCES public.courses(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  order_index INTEGER NOT NULL DEFAULT 0,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Lessons
CREATE TABLE public.lessons (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  module_id UUID NOT NULL REFERENCES public.modules(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  order_index INTEGER NOT NULL DEFAULT 0,
  is_locked BOOLEAN NOT NULL DEFAULT false,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Lesson content blocks (polymorphic content)
CREATE TABLE public.lesson_content (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  lesson_id UUID NOT NULL REFERENCES public.lessons(id) ON DELETE CASCADE,
  type content_type NOT NULL,
  content_payload JSONB NOT NULL,
  order_index INTEGER NOT NULL DEFAULT 0,
  exercise_type TEXT, -- For exercise content: 'quiz', 'listening', 'writing', 'fill_blank'
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Lesson downloadable resources
CREATE TABLE public.lesson_resources (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  lesson_id UUID NOT NULL REFERENCES public.lessons(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  file_url TEXT NOT NULL, -- Local path (e.g., '/uploads/resources/abc123.pdf')
  file_type TEXT NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- ============================================
-- CLASSROOM TABLES
-- ============================================

-- Groups (learning cohorts)
CREATE TABLE public.groups (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  teacher_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE RESTRICT,
  schedule_info TEXT,
  is_active BOOLEAN DEFAULT true,
  start_date DATE DEFAULT CURRENT_DATE,
  schedule_days TEXT[] DEFAULT '{}',
  start_time TIME,
  end_time TIME,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Group-Student enrollment (many-to-many)
CREATE TABLE public.group_students (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  group_id UUID NOT NULL REFERENCES public.groups(id) ON DELETE CASCADE,
  student_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  joined_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  UNIQUE(group_id, student_id)
);

-- Group-Course assignment (many-to-many)
CREATE TABLE public.group_courses (
  group_id UUID NOT NULL REFERENCES public.groups(id) ON DELETE CASCADE,
  course_id UUID NOT NULL REFERENCES public.courses(id) ON DELETE CASCADE,
  assigned_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
  PRIMARY KEY (group_id, course_id)
);

-- Attendance records
CREATE TABLE public.attendance (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  group_id UUID NOT NULL REFERENCES public.groups(id) ON DELETE CASCADE,
  student_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  date DATE NOT NULL DEFAULT CURRENT_DATE,
  status attendance_status NOT NULL DEFAULT 'present',
  notes TEXT,
  created_by UUID REFERENCES public.profiles(id),
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  UNIQUE(group_id, student_id, date)
);

-- ============================================
-- PROGRESS TRACKING
-- ============================================

-- Individual course enrollments (student self-enrollment)
CREATE TABLE public.enrollments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  course_id UUID NOT NULL REFERENCES public.courses(id) ON DELETE CASCADE,
  enrolled_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  UNIQUE(user_id, course_id)
);

-- Lesson completions
CREATE TABLE public.lesson_completions (
  user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  lesson_id UUID NOT NULL REFERENCES public.lessons(id) ON DELETE CASCADE,
  completed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, lesson_id)
);

-- Quiz/Exercise results
CREATE TABLE public.quiz_results (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  lesson_id UUID NOT NULL REFERENCES public.lessons(id) ON DELETE CASCADE,
  content_id UUID REFERENCES public.lesson_content(id) ON DELETE SET NULL,
  score INTEGER NOT NULL CHECK (score >= 0 AND score <= 100),
  passed BOOLEAN NOT NULL DEFAULT false,
  total_questions INTEGER DEFAULT 0,
  mistakes_log JSONB, -- { questionId: userAnswer, ... }
  metadata JSONB,
  teacher_feedback TEXT,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

-- Manual lesson unlocks by teachers
CREATE TABLE public.student_lesson_access (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  student_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  lesson_id UUID NOT NULL REFERENCES public.lessons(id) ON DELETE CASCADE,
  is_unlocked BOOLEAN DEFAULT false,
  unlocked_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
  created_by UUID REFERENCES public.profiles(id),
  UNIQUE(student_id, lesson_id)
);

-- ============================================
-- LIVE LESSON MODE
-- ============================================

-- A synchronous, teacher-driven walkthrough of a lesson in a classroom.
-- `classroom_id` references public.groups (the codebase calls a classroom a "group";
-- this feature exposes it as "classroom" on the wire).
CREATE TABLE public.live_sessions (
  id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  classroom_id         UUID NOT NULL REFERENCES public.groups(id) ON DELETE CASCADE,
  lesson_id            UUID NOT NULL REFERENCES public.lessons(id) ON DELETE RESTRICT,
  teacher_id           UUID NOT NULL REFERENCES public.profiles(id) ON DELETE RESTRICT,
  started_at           TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  ended_at             TIMESTAMP WITH TIME ZONE,
  current_block_index  INTEGER NOT NULL DEFAULT 0,
  end_reason           TEXT  -- one of: 'teacher_ended', 'timeout', 'server_restart'
);

CREATE INDEX ix_live_sessions_classroom_ended_at
  ON public.live_sessions (classroom_id, ended_at);

CREATE INDEX ix_live_sessions_teacher_started_at
  ON public.live_sessions (teacher_id, started_at DESC);

-- Enforce "only one active session per classroom" at the DB layer so concurrent
-- POST /api/live-sessions calls race on the index, not on application logic.
CREATE UNIQUE INDEX ix_live_sessions_one_active_per_classroom
  ON public.live_sessions (classroom_id) WHERE ended_at IS NULL;

-- ============================================
-- PAYMENTS
-- ============================================

CREATE TABLE public.payments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
  course_id UUID NOT NULL REFERENCES public.courses(id) ON DELETE RESTRICT,
  amount NUMERIC NOT NULL,
  currency VARCHAR DEFAULT 'USD',
  status VARCHAR DEFAULT 'pending' CHECK (status IN ('completed', 'pending', 'failed')),
  period_start TIMESTAMP WITH TIME ZONE NOT NULL,
  period_end TIMESTAMP WITH TIME ZONE NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT now()
);

-- ============================================
-- INDEXES FOR PERFORMANCE
-- ============================================

-- Profiles indexes
CREATE INDEX idx_profiles_email ON public.profiles(email);
CREATE INDEX idx_profiles_role ON public.profiles(role);
CREATE INDEX idx_profiles_is_active ON public.profiles(is_active);

-- Sessions indexes
CREATE INDEX idx_sessions_user_id ON public.sessions(user_id);
CREATE INDEX idx_sessions_refresh_token ON public.sessions(refresh_token);
CREATE INDEX idx_sessions_expires_at ON public.sessions(expires_at);

-- Curriculum indexes
CREATE INDEX idx_modules_course_id ON public.modules(course_id);
CREATE INDEX idx_lessons_module_id ON public.lessons(module_id);
CREATE INDEX idx_lesson_content_lesson_id ON public.lesson_content(lesson_id);
CREATE INDEX idx_lesson_resources_lesson_id ON public.lesson_resources(lesson_id);

-- Classroom indexes
CREATE INDEX idx_groups_teacher_id ON public.groups(teacher_id);
CREATE INDEX idx_group_students_group_id ON public.group_students(group_id);
CREATE INDEX idx_group_students_student_id ON public.group_students(student_id);
CREATE INDEX idx_group_courses_group_id ON public.group_courses(group_id);
CREATE INDEX idx_group_courses_course_id ON public.group_courses(course_id);
CREATE INDEX idx_attendance_group_id ON public.attendance(group_id);
CREATE INDEX idx_attendance_student_id ON public.attendance(student_id);
CREATE INDEX idx_attendance_date ON public.attendance(date);

-- Progress indexes
CREATE INDEX idx_enrollments_user_id ON public.enrollments(user_id);
CREATE INDEX idx_enrollments_course_id ON public.enrollments(course_id);
CREATE INDEX idx_lesson_completions_user_id ON public.lesson_completions(user_id);
CREATE INDEX idx_lesson_completions_lesson_id ON public.lesson_completions(lesson_id);
CREATE INDEX idx_quiz_results_user_id ON public.quiz_results(user_id);
CREATE INDEX idx_quiz_results_lesson_id ON public.quiz_results(lesson_id);
CREATE INDEX idx_student_lesson_access_student_id ON public.student_lesson_access(student_id);

-- Payments indexes
CREATE INDEX idx_payments_user_id ON public.payments(user_id);
CREATE INDEX idx_payments_course_id ON public.payments(course_id);
CREATE INDEX idx_payments_status ON public.payments(status);

-- ============================================
-- TRIGGERS
-- ============================================

-- Auto-update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_profiles_updated_at
  BEFORE UPDATE ON public.profiles
  FOR EACH ROW
  EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_courses_updated_at
  BEFORE UPDATE ON public.courses
  FOR EACH ROW
  EXECUTE FUNCTION update_updated_at_column();

-- ============================================
-- DEFAULT ADMIN USER (Change password after first login!)
-- ============================================

-- Default password: 'admin123' (bcrypt hash with salt rounds=10)
-- IMPORTANT: Change this immediately after deployment!
INSERT INTO public.profiles (email, password_hash, full_name, role, is_active, email_verified)
VALUES (
  'admin@langartlms.com',
  '$2b$10$YourHashHere', -- Replace with actual bcrypt hash
  'System Administrator',
  'admin',
  true,
  true
) ON CONFLICT (email) DO NOTHING;

-- ============================================
-- NOTES
-- ============================================

/*
MIGRATION FROM SUPABASE:

1. Export your Supabase data:
   - Use Supabase dashboard → Database → Backups
   - Or pg_dump with proper credentials

2. Transform auth.users to profiles:
   - Map auth.users.id → profiles.id
   - Map auth.users.email → profiles.email
   - Generate temporary passwords or send password reset emails

3. File URLs:
   - Update all file_url and thumbnail_url paths from Supabase Storage to local paths
   - Download files from Supabase Storage and upload to backend/uploads/

4. Run this schema on fresh PostgreSQL database
5. Import transformed data
6. Update frontend API URLs

SECURITY NOTES:
- Change default admin password immediately
- Use strong JWT secrets (set in .env)
- Enable SSL/TLS in production
- Regular backups with pg_dump or pg_basebackup
*/
