--
-- PostgreSQL database dump
--

\restrict jZmKgBTcI1RJG8PCnzJ2vW4ogNd1uurYuZdGRRhy0VIfU6VewhvQC5DH4YzRt7J

-- Dumped from database version 15.16
-- Dumped by pg_dump version 15.16

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: AttendanceStatus; Type: TYPE; Schema: public; Owner: langartuser
--

CREATE TYPE public."AttendanceStatus" AS ENUM (
    'present',
    'absent',
    'late',
    'excused'
);


ALTER TYPE public."AttendanceStatus" OWNER TO langartuser;

--
-- Name: ContentType; Type: TYPE; Schema: public; Owner: langartuser
--

CREATE TYPE public."ContentType" AS ENUM (
    'text',
    'video',
    'audio',
    'slide',
    'exercise'
);


ALTER TYPE public."ContentType" OWNER TO langartuser;

--
-- Name: PaymentStatus; Type: TYPE; Schema: public; Owner: langartuser
--

CREATE TYPE public."PaymentStatus" AS ENUM (
    'completed',
    'pending',
    'failed'
);


ALTER TYPE public."PaymentStatus" OWNER TO langartuser;

--
-- Name: Role; Type: TYPE; Schema: public; Owner: langartuser
--

CREATE TYPE public."Role" AS ENUM (
    'admin',
    'teacher',
    'student'
);


ALTER TYPE public."Role" OWNER TO langartuser;

--
-- Name: update_updated_at_column(); Type: FUNCTION; Schema: public; Owner: langartuser
--

CREATE FUNCTION public.update_updated_at_column() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$;


ALTER FUNCTION public.update_updated_at_column() OWNER TO langartuser;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: attendance; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.attendance (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    group_id uuid NOT NULL,
    student_id uuid NOT NULL,
    date date DEFAULT CURRENT_TIMESTAMP NOT NULL,
    notes text,
    created_by uuid,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    status public."AttendanceStatus" DEFAULT 'present'::public."AttendanceStatus" NOT NULL
);


ALTER TABLE public.attendance OWNER TO langartuser;

--
-- Name: courses; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.courses (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    title text NOT NULL,
    description text,
    thumbnail_url text,
    price_monthly numeric,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.courses OWNER TO langartuser;

--
-- Name: enrollments; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.enrollments (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    course_id uuid NOT NULL,
    enrolled_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.enrollments OWNER TO langartuser;

--
-- Name: group_courses; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.group_courses (
    group_id uuid NOT NULL,
    course_id uuid NOT NULL,
    assigned_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.group_courses OWNER TO langartuser;

--
-- Name: group_students; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.group_students (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    group_id uuid NOT NULL,
    student_id uuid NOT NULL,
    joined_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.group_students OWNER TO langartuser;

--
-- Name: groups; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.groups (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name text NOT NULL,
    teacher_id uuid NOT NULL,
    schedule_info text,
    is_active boolean DEFAULT true NOT NULL,
    start_date date DEFAULT CURRENT_TIMESTAMP,
    schedule_days text[] DEFAULT '{}'::text[],
    start_time time without time zone,
    end_time time without time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.groups OWNER TO langartuser;

--
-- Name: lesson_completions; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.lesson_completions (
    user_id uuid NOT NULL,
    lesson_id uuid NOT NULL,
    completed_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.lesson_completions OWNER TO langartuser;

--
-- Name: lesson_content; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.lesson_content (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    lesson_id uuid NOT NULL,
    content_payload jsonb NOT NULL,
    order_index integer DEFAULT 0 NOT NULL,
    exercise_type text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    type public."ContentType" NOT NULL
);


ALTER TABLE public.lesson_content OWNER TO langartuser;

--
-- Name: lesson_resources; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.lesson_resources (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    lesson_id uuid NOT NULL,
    title text NOT NULL,
    file_url text NOT NULL,
    file_type text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.lesson_resources OWNER TO langartuser;

--
-- Name: lessons; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.lessons (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    module_id uuid NOT NULL,
    title text NOT NULL,
    order_index integer DEFAULT 0 NOT NULL,
    is_locked boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.lessons OWNER TO langartuser;

--
-- Name: modules; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.modules (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    course_id uuid NOT NULL,
    title text NOT NULL,
    order_index integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.modules OWNER TO langartuser;

--
-- Name: payments; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.payments (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    course_id uuid NOT NULL,
    amount numeric NOT NULL,
    currency text DEFAULT 'USD'::character varying NOT NULL,
    period_start timestamp with time zone NOT NULL,
    period_end timestamp with time zone NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    status public."PaymentStatus" DEFAULT 'pending'::public."PaymentStatus" NOT NULL
);


ALTER TABLE public.payments OWNER TO langartuser;

--
-- Name: profiles; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.profiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    email text NOT NULL,
    password_hash text NOT NULL,
    full_name text DEFAULT ''::text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    email_verified boolean DEFAULT false NOT NULL,
    last_login timestamp with time zone,
    reset_token text,
    reset_token_expires timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    role public."Role" DEFAULT 'student'::public."Role" NOT NULL
);


ALTER TABLE public.profiles OWNER TO langartuser;

--
-- Name: quiz_results; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.quiz_results (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    lesson_id uuid NOT NULL,
    content_id uuid,
    score integer NOT NULL,
    passed boolean DEFAULT false NOT NULL,
    total_questions integer DEFAULT 0 NOT NULL,
    mistakes_log jsonb,
    metadata jsonb,
    teacher_feedback text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT quiz_results_score_check CHECK (((score >= 0) AND (score <= 100)))
);


ALTER TABLE public.quiz_results OWNER TO langartuser;

--
-- Name: sessions; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.sessions (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    refresh_token text NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    last_used_at timestamp with time zone DEFAULT now() NOT NULL,
    user_agent text,
    ip_address text
);


ALTER TABLE public.sessions OWNER TO langartuser;

--
-- Name: student_lesson_access; Type: TABLE; Schema: public; Owner: langartuser
--

CREATE TABLE public.student_lesson_access (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    student_id uuid NOT NULL,
    lesson_id uuid NOT NULL,
    is_unlocked boolean DEFAULT false NOT NULL,
    unlocked_at timestamp with time zone DEFAULT now() NOT NULL,
    created_by uuid
);


ALTER TABLE public.student_lesson_access OWNER TO langartuser;

--
-- Data for Name: attendance; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.attendance (id, group_id, student_id, date, notes, created_by, created_at, status) FROM stdin;
\.


--
-- Data for Name: courses; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.courses (id, title, description, thumbnail_url, price_monthly, created_at, updated_at) FROM stdin;
00000000-0000-0000-0000-000000000001	English for Beginners	Learn English from scratch with interactive lessons	\N	99.98999999999999	2026-02-17 11:12:40.862+00	2026-02-17 11:12:40.862+00
00000000-0000-0000-0000-000000000002	Spanish Intermediate	Improve your Spanish skills	\N	149.99	2026-02-17 11:12:40.868+00	2026-02-17 11:12:40.868+00
00000000-0000-0000-0000-000000000003	German Advanced	Master advanced German grammar and vocabulary	\N	199.99	2026-02-17 11:12:40.872+00	2026-02-17 11:12:40.872+00
\.


--
-- Data for Name: enrollments; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.enrollments (id, user_id, course_id, enrolled_at) FROM stdin;
9fa0685f-7033-44dd-b3c2-1db8c0f94784	085ca90e-00ac-4ec3-afed-5a93a8397cae	00000000-0000-0000-0000-000000000001	2026-02-17 11:12:40.878+00
9d195f4f-455a-409a-acd6-6b120626ecbf	1433dd30-0e69-4c05-ba7c-93c760de221f	00000000-0000-0000-0000-000000000001	2026-02-17 11:12:40.88+00
103665cc-d840-4a03-b14a-f8b0504a546b	3f68113d-791d-4a3f-92ed-f1d16b56fe54	00000000-0000-0000-0000-000000000001	2026-02-17 11:12:40.881+00
eb38157a-ee76-41a7-a76e-c540fa2ae879	623b4340-f17a-4285-9032-1c20dd5932b7	00000000-0000-0000-0000-000000000001	2026-02-17 11:12:40.883+00
ea60f1b7-115d-4ebf-9efb-f9a4cd4e1e93	654d5da6-9133-4a60-bd27-deb7aff6686d	00000000-0000-0000-0000-000000000001	2026-02-17 11:12:40.884+00
d4e7f03d-0d4b-4d28-ad6c-5569ee304dbf	78badad5-1b33-4bb5-808c-29d182152d22	00000000-0000-0000-0000-000000000002	2026-02-17 11:12:40.889+00
edeebc97-6474-4999-8971-82cf8fd9d6aa	a6946728-e418-45f6-ad20-81616e26f7b0	00000000-0000-0000-0000-000000000002	2026-02-17 11:12:40.89+00
6721c09a-5640-402c-8fd8-a32dfcbc0e1c	b498b3c2-ca5b-4fde-ab88-1451851d5084	00000000-0000-0000-0000-000000000002	2026-02-17 11:12:40.891+00
cab8459d-c5cf-4dd0-889c-c2dc3fda51e3	d0e46db7-676a-4d3e-a747-02b605d7d5c3	00000000-0000-0000-0000-000000000002	2026-02-17 11:12:40.892+00
fc08659e-d7ea-42e4-a733-5e4a21a94d67	d9d61202-6e64-4355-ab42-2631bea54fcd	00000000-0000-0000-0000-000000000002	2026-02-17 11:12:40.893+00
\.


--
-- Data for Name: group_courses; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.group_courses (group_id, course_id, assigned_at) FROM stdin;
00000000-0000-0000-0000-000000000101	00000000-0000-0000-0000-000000000001	2026-02-17 11:12:40.886+00
00000000-0000-0000-0000-000000000102	00000000-0000-0000-0000-000000000002	2026-02-17 11:12:40.894+00
\.


--
-- Data for Name: group_students; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.group_students (id, group_id, student_id, joined_at) FROM stdin;
f8f6f037-5e41-4ee2-a7d3-4295869bb651	00000000-0000-0000-0000-000000000101	085ca90e-00ac-4ec3-afed-5a93a8397cae	2026-02-17 11:12:40.876+00
50d0ad6f-d683-4fa8-9e7d-6aead39b18f3	00000000-0000-0000-0000-000000000101	1433dd30-0e69-4c05-ba7c-93c760de221f	2026-02-17 11:12:40.879+00
1c00745b-2b86-409b-9d22-a8327e2f4ac2	00000000-0000-0000-0000-000000000101	3f68113d-791d-4a3f-92ed-f1d16b56fe54	2026-02-17 11:12:40.88+00
053201fb-5db9-42e0-ae86-4c1f5978b63b	00000000-0000-0000-0000-000000000101	623b4340-f17a-4285-9032-1c20dd5932b7	2026-02-17 11:12:40.882+00
7559b97a-de26-4886-a0e5-5d1259e00f27	00000000-0000-0000-0000-000000000101	654d5da6-9133-4a60-bd27-deb7aff6686d	2026-02-17 11:12:40.884+00
0fc5984f-9b2f-489f-b201-677032eee3aa	00000000-0000-0000-0000-000000000102	78badad5-1b33-4bb5-808c-29d182152d22	2026-02-17 11:12:40.888+00
4c2a8380-9d4a-4c64-9911-746a91eab3b8	00000000-0000-0000-0000-000000000102	a6946728-e418-45f6-ad20-81616e26f7b0	2026-02-17 11:12:40.889+00
a05a529c-41f1-4121-88bd-98221dbe62ca	00000000-0000-0000-0000-000000000102	b498b3c2-ca5b-4fde-ab88-1451851d5084	2026-02-17 11:12:40.89+00
bea68a87-f07b-426a-adf9-34315cee8b58	00000000-0000-0000-0000-000000000102	d0e46db7-676a-4d3e-a747-02b605d7d5c3	2026-02-17 11:12:40.892+00
f888372c-ac8d-41f4-9011-0efca25b5ff1	00000000-0000-0000-0000-000000000102	d9d61202-6e64-4355-ab42-2631bea54fcd	2026-02-17 11:12:40.893+00
\.


--
-- Data for Name: groups; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.groups (id, name, teacher_id, schedule_info, is_active, start_date, schedule_days, start_time, end_time, created_at) FROM stdin;
00000000-0000-0000-0000-000000000101	English Beginners - Morning Class	8c349938-a698-4d50-b046-5056937e413b	Monday, Wednesday, Friday 9:00-11:00	t	2026-02-17	{monday,wednesday,friday}	\N	\N	2026-02-17 11:12:40.875+00
00000000-0000-0000-0000-000000000102	Spanish Intermediate - Evening Class	a6f3bca2-5d30-4679-9376-e37357a2ab93	Tuesday, Thursday 18:00-20:00	t	2026-02-17	{tuesday,thursday}	\N	\N	2026-02-17 11:12:40.887+00
\.


--
-- Data for Name: lesson_completions; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.lesson_completions (user_id, lesson_id, completed_at) FROM stdin;
085ca90e-00ac-4ec3-afed-5a93a8397cae	00000000-0000-0000-0000-000000000111	2026-01-24 16:58:39.551+00
1433dd30-0e69-4c05-ba7c-93c760de221f	00000000-0000-0000-0000-000000000111	2026-01-29 20:21:22.82+00
623b4340-f17a-4285-9032-1c20dd5932b7	00000000-0000-0000-0000-000000000111	2026-02-06 00:51:06.572+00
623b4340-f17a-4285-9032-1c20dd5932b7	00000000-0000-0000-0000-000000000112	2026-02-10 20:05:04.638+00
085ca90e-00ac-4ec3-afed-5a93a8397cae	00000000-0000-0000-0000-000000000112	2026-01-27 20:33:17.319+00
3f68113d-791d-4a3f-92ed-f1d16b56fe54	00000000-0000-0000-0000-000000000111	2026-02-03 12:44:01.637+00
3f68113d-791d-4a3f-92ed-f1d16b56fe54	00000000-0000-0000-0000-000000000112	2026-02-01 15:49:05.928+00
654d5da6-9133-4a60-bd27-deb7aff6686d	00000000-0000-0000-0000-000000000111	2026-02-15 16:28:22.491+00
\.


--
-- Data for Name: lesson_content; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.lesson_content (id, lesson_id, content_payload, order_index, exercise_type, created_at, type) FROM stdin;
\.


--
-- Data for Name: lesson_resources; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.lesson_resources (id, lesson_id, title, file_url, file_type, created_at) FROM stdin;
\.


--
-- Data for Name: lessons; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.lessons (id, module_id, title, order_index, is_locked, created_at) FROM stdin;
00000000-0000-0000-0000-000000000111	00000000-0000-0000-0000-000000000011	Alphabet and Pronunciation	1	f	2026-02-17 11:12:40.864+00
00000000-0000-0000-0000-000000000112	00000000-0000-0000-0000-000000000011	Basic Greetings	2	f	2026-02-17 11:12:40.865+00
00000000-0000-0000-0000-000000000113	00000000-0000-0000-0000-000000000011	Quiz: Module 1	3	f	2026-02-17 11:12:40.866+00
00000000-0000-0000-0000-000000000121	00000000-0000-0000-0000-000000000012	Present Simple Tense	1	f	2026-02-17 11:12:40.868+00
00000000-0000-0000-0000-000000000211	00000000-0000-0000-0000-000000000021	At the Restaurant	1	f	2026-02-17 11:12:40.871+00
\.


--
-- Data for Name: modules; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.modules (id, course_id, title, order_index, created_at) FROM stdin;
00000000-0000-0000-0000-000000000011	00000000-0000-0000-0000-000000000001	Introduction to English	1	2026-02-17 11:12:40.863+00
00000000-0000-0000-0000-000000000012	00000000-0000-0000-0000-000000000001	Grammar Basics	2	2026-02-17 11:12:40.867+00
00000000-0000-0000-0000-000000000021	00000000-0000-0000-0000-000000000002	Conversational Spanish	1	2026-02-17 11:12:40.87+00
\.


--
-- Data for Name: payments; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.payments (id, user_id, course_id, amount, currency, period_start, period_end, created_at, status) FROM stdin;
16a74691-3f5f-4f97-b090-db073a6e374a	085ca90e-00ac-4ec3-afed-5a93a8397cae	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.878+00	2026-03-17 11:12:40.878+00	2026-02-17 11:12:40.901+00	completed
23cddc35-f2a8-4fb7-af92-61cf6966a963	1433dd30-0e69-4c05-ba7c-93c760de221f	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.88+00	2026-03-17 11:12:40.88+00	2026-02-17 11:12:40.903+00	completed
e254a899-f0fe-4df6-95c7-cc4e5131017a	3f68113d-791d-4a3f-92ed-f1d16b56fe54	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.881+00	2026-03-17 11:12:40.881+00	2026-02-17 11:12:40.904+00	completed
866c3d19-68b4-4a39-ab77-4fd5b85147e6	623b4340-f17a-4285-9032-1c20dd5932b7	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.883+00	2026-03-17 11:12:40.883+00	2026-02-17 11:12:40.904+00	completed
ac8ef53f-9f46-4f4b-ae92-fd4fc83ffff8	654d5da6-9133-4a60-bd27-deb7aff6686d	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.884+00	2026-03-17 11:12:40.884+00	2026-02-17 11:12:40.905+00	completed
f6994683-250b-4f83-8da5-fc4050545b8e	78badad5-1b33-4bb5-808c-29d182152d22	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.889+00	2026-03-17 11:12:40.889+00	2026-02-17 11:12:40.905+00	completed
9ebe8b7a-16db-42cf-8a04-41933cfebe30	a6946728-e418-45f6-ad20-81616e26f7b0	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.89+00	2026-03-17 11:12:40.89+00	2026-02-17 11:12:40.905+00	completed
b81cdbac-5e47-42c2-a3ad-9e262b6b11ca	b498b3c2-ca5b-4fde-ab88-1451851d5084	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.891+00	2026-03-17 11:12:40.891+00	2026-02-17 11:12:40.906+00	completed
c9e03637-7a70-4441-93b6-193782485a47	d0e46db7-676a-4d3e-a747-02b605d7d5c3	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.892+00	2026-03-17 11:12:40.892+00	2026-02-17 11:12:40.906+00	pending
3398ea2d-5cca-49db-a850-d78c36bd0cf1	d9d61202-6e64-4355-ab42-2631bea54fcd	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.893+00	2026-03-17 11:12:40.893+00	2026-02-17 11:12:40.906+00	completed
7d807c08-5d03-4cdc-9ff8-c3fb0986a5de	085ca90e-00ac-4ec3-afed-5a93a8397cae	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.878+00	2026-03-17 11:12:40.878+00	2026-02-17 11:13:03.3+00	completed
1bcd5bd7-957a-4237-b048-48f551f0c069	1433dd30-0e69-4c05-ba7c-93c760de221f	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.88+00	2026-03-17 11:12:40.88+00	2026-02-17 11:13:03.301+00	pending
f58ed5d5-0262-4fa4-bac3-d28a158dff00	3f68113d-791d-4a3f-92ed-f1d16b56fe54	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.881+00	2026-03-17 11:12:40.881+00	2026-02-17 11:13:03.302+00	completed
e4edc264-3acd-481b-8bc5-0a638b03aacd	623b4340-f17a-4285-9032-1c20dd5932b7	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.883+00	2026-03-17 11:12:40.883+00	2026-02-17 11:13:03.303+00	completed
20f6f5c0-af45-4dac-88bf-138bd446e833	654d5da6-9133-4a60-bd27-deb7aff6686d	00000000-0000-0000-0000-000000000001	99.98999999999999	USD	2026-02-17 11:12:40.884+00	2026-03-17 11:12:40.884+00	2026-02-17 11:13:03.303+00	pending
efe6f303-7f4e-4651-8440-aab3f7a31ab6	78badad5-1b33-4bb5-808c-29d182152d22	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.889+00	2026-03-17 11:12:40.889+00	2026-02-17 11:13:03.303+00	pending
ed27cb81-cfd4-4f56-828e-0c2e04e26176	a6946728-e418-45f6-ad20-81616e26f7b0	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.89+00	2026-03-17 11:12:40.89+00	2026-02-17 11:13:03.304+00	completed
4080f006-9010-45bf-9dc3-0f5c5b54c1f9	b498b3c2-ca5b-4fde-ab88-1451851d5084	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.891+00	2026-03-17 11:12:40.891+00	2026-02-17 11:13:03.304+00	failed
1d4442d1-d776-4697-9b98-af1068f3df78	d0e46db7-676a-4d3e-a747-02b605d7d5c3	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.892+00	2026-03-17 11:12:40.892+00	2026-02-17 11:13:03.305+00	completed
ee4f4c16-881e-48cd-afd1-b1528950d8b6	d9d61202-6e64-4355-ab42-2631bea54fcd	00000000-0000-0000-0000-000000000002	149.99	USD	2026-02-17 11:12:40.893+00	2026-03-17 11:12:40.893+00	2026-02-17 11:13:03.305+00	completed
\.


--
-- Data for Name: profiles; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.profiles (id, email, password_hash, full_name, is_active, email_verified, last_login, reset_token, reset_token_expires, created_at, updated_at, role) FROM stdin;
8d5fd92f-5171-4d03-abe7-72cca4e681d3	admin@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Admin User	t	f	\N	\N	\N	2026-02-17 11:12:40.833+00	2026-02-17 11:12:40.833+00	admin
8c349938-a698-4d50-b046-5056937e413b	teacher1@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	John Smith	t	f	\N	\N	\N	2026-02-17 11:12:40.844+00	2026-02-17 11:12:40.844+00	teacher
a6f3bca2-5d30-4679-9376-e37357a2ab93	teacher2@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Sarah Johnson	t	f	\N	\N	\N	2026-02-17 11:12:40.845+00	2026-02-17 11:12:40.845+00	teacher
1433dd30-0e69-4c05-ba7c-93c760de221f	student1@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 1	t	f	\N	\N	\N	2026-02-17 11:12:40.847+00	2026-02-17 11:12:40.847+00	student
d0e46db7-676a-4d3e-a747-02b605d7d5c3	student2@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 2	t	f	\N	\N	\N	2026-02-17 11:12:40.848+00	2026-02-17 11:12:40.848+00	student
085ca90e-00ac-4ec3-afed-5a93a8397cae	student3@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 3	t	f	\N	\N	\N	2026-02-17 11:12:40.849+00	2026-02-17 11:12:40.849+00	student
623b4340-f17a-4285-9032-1c20dd5932b7	student4@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 4	t	f	\N	\N	\N	2026-02-17 11:12:40.852+00	2026-02-17 11:12:40.852+00	student
78badad5-1b33-4bb5-808c-29d182152d22	student5@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 5	t	f	\N	\N	\N	2026-02-17 11:12:40.856+00	2026-02-17 11:12:40.856+00	student
b498b3c2-ca5b-4fde-ab88-1451851d5084	student6@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 6	t	f	\N	\N	\N	2026-02-17 11:12:40.857+00	2026-02-17 11:12:40.857+00	student
3f68113d-791d-4a3f-92ed-f1d16b56fe54	student7@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 7	t	f	\N	\N	\N	2026-02-17 11:12:40.858+00	2026-02-17 11:12:40.858+00	student
654d5da6-9133-4a60-bd27-deb7aff6686d	student8@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 8	t	f	\N	\N	\N	2026-02-17 11:12:40.859+00	2026-02-17 11:12:40.859+00	student
a6946728-e418-45f6-ad20-81616e26f7b0	student9@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 9	t	f	\N	\N	\N	2026-02-17 11:12:40.86+00	2026-02-17 11:12:40.86+00	student
d9d61202-6e64-4355-ab42-2631bea54fcd	student10@langart.com	$2b$10$tqs12NkWPp/MSCktrHExGu3SQ2dl58wQs5UmRJXGtUNQ6xMgRX8pe	Student 10	t	f	\N	\N	\N	2026-02-17 11:12:40.861+00	2026-02-17 11:12:40.861+00	student
\.


--
-- Data for Name: quiz_results; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.quiz_results (id, user_id, lesson_id, content_id, score, passed, total_questions, mistakes_log, metadata, teacher_feedback, created_at) FROM stdin;
\.


--
-- Data for Name: sessions; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.sessions (id, user_id, refresh_token, expires_at, created_at, last_used_at, user_agent, ip_address) FROM stdin;
\.


--
-- Data for Name: student_lesson_access; Type: TABLE DATA; Schema: public; Owner: langartuser
--

COPY public.student_lesson_access (id, student_id, lesson_id, is_unlocked, unlocked_at, created_by) FROM stdin;
\.


--
-- Name: attendance attendance_group_id_student_id_date_key; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.attendance
    ADD CONSTRAINT attendance_group_id_student_id_date_key UNIQUE (group_id, student_id, date);


--
-- Name: attendance attendance_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.attendance
    ADD CONSTRAINT attendance_pkey PRIMARY KEY (id);


--
-- Name: courses courses_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.courses
    ADD CONSTRAINT courses_pkey PRIMARY KEY (id);


--
-- Name: enrollments enrollments_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.enrollments
    ADD CONSTRAINT enrollments_pkey PRIMARY KEY (id);


--
-- Name: enrollments enrollments_user_id_course_id_key; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.enrollments
    ADD CONSTRAINT enrollments_user_id_course_id_key UNIQUE (user_id, course_id);


--
-- Name: group_courses group_courses_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_courses
    ADD CONSTRAINT group_courses_pkey PRIMARY KEY (group_id, course_id);


--
-- Name: group_students group_students_group_id_student_id_key; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_students
    ADD CONSTRAINT group_students_group_id_student_id_key UNIQUE (group_id, student_id);


--
-- Name: group_students group_students_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_students
    ADD CONSTRAINT group_students_pkey PRIMARY KEY (id);


--
-- Name: groups groups_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.groups
    ADD CONSTRAINT groups_pkey PRIMARY KEY (id);


--
-- Name: lesson_completions lesson_completions_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_completions
    ADD CONSTRAINT lesson_completions_pkey PRIMARY KEY (user_id, lesson_id);


--
-- Name: lesson_content lesson_content_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_content
    ADD CONSTRAINT lesson_content_pkey PRIMARY KEY (id);


--
-- Name: lesson_resources lesson_resources_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_resources
    ADD CONSTRAINT lesson_resources_pkey PRIMARY KEY (id);


--
-- Name: lessons lessons_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lessons
    ADD CONSTRAINT lessons_pkey PRIMARY KEY (id);


--
-- Name: modules modules_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.modules
    ADD CONSTRAINT modules_pkey PRIMARY KEY (id);


--
-- Name: payments payments_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT payments_pkey PRIMARY KEY (id);


--
-- Name: profiles profiles_email_key; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.profiles
    ADD CONSTRAINT profiles_email_key UNIQUE (email);


--
-- Name: profiles profiles_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.profiles
    ADD CONSTRAINT profiles_pkey PRIMARY KEY (id);


--
-- Name: quiz_results quiz_results_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.quiz_results
    ADD CONSTRAINT quiz_results_pkey PRIMARY KEY (id);


--
-- Name: sessions sessions_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.sessions
    ADD CONSTRAINT sessions_pkey PRIMARY KEY (id);


--
-- Name: sessions sessions_refresh_token_key; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.sessions
    ADD CONSTRAINT sessions_refresh_token_key UNIQUE (refresh_token);


--
-- Name: student_lesson_access student_lesson_access_pkey; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.student_lesson_access
    ADD CONSTRAINT student_lesson_access_pkey PRIMARY KEY (id);


--
-- Name: student_lesson_access student_lesson_access_student_id_lesson_id_key; Type: CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.student_lesson_access
    ADD CONSTRAINT student_lesson_access_student_id_lesson_id_key UNIQUE (student_id, lesson_id);


--
-- Name: courses update_courses_updated_at; Type: TRIGGER; Schema: public; Owner: langartuser
--

CREATE TRIGGER update_courses_updated_at BEFORE UPDATE ON public.courses FOR EACH ROW EXECUTE FUNCTION public.update_updated_at_column();


--
-- Name: profiles update_profiles_updated_at; Type: TRIGGER; Schema: public; Owner: langartuser
--

CREATE TRIGGER update_profiles_updated_at BEFORE UPDATE ON public.profiles FOR EACH ROW EXECUTE FUNCTION public.update_updated_at_column();


--
-- Name: attendance attendance_created_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.attendance
    ADD CONSTRAINT attendance_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: attendance attendance_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.attendance
    ADD CONSTRAINT attendance_group_id_fkey FOREIGN KEY (group_id) REFERENCES public.groups(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: attendance attendance_student_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.attendance
    ADD CONSTRAINT attendance_student_id_fkey FOREIGN KEY (student_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: enrollments enrollments_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.enrollments
    ADD CONSTRAINT enrollments_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: enrollments enrollments_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.enrollments
    ADD CONSTRAINT enrollments_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: group_courses group_courses_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_courses
    ADD CONSTRAINT group_courses_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: group_courses group_courses_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_courses
    ADD CONSTRAINT group_courses_group_id_fkey FOREIGN KEY (group_id) REFERENCES public.groups(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: group_students group_students_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_students
    ADD CONSTRAINT group_students_group_id_fkey FOREIGN KEY (group_id) REFERENCES public.groups(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: group_students group_students_student_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.group_students
    ADD CONSTRAINT group_students_student_id_fkey FOREIGN KEY (student_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: groups groups_teacher_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.groups
    ADD CONSTRAINT groups_teacher_id_fkey FOREIGN KEY (teacher_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: lesson_completions lesson_completions_lesson_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_completions
    ADD CONSTRAINT lesson_completions_lesson_id_fkey FOREIGN KEY (lesson_id) REFERENCES public.lessons(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: lesson_completions lesson_completions_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_completions
    ADD CONSTRAINT lesson_completions_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: lesson_content lesson_content_lesson_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_content
    ADD CONSTRAINT lesson_content_lesson_id_fkey FOREIGN KEY (lesson_id) REFERENCES public.lessons(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: lesson_resources lesson_resources_lesson_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lesson_resources
    ADD CONSTRAINT lesson_resources_lesson_id_fkey FOREIGN KEY (lesson_id) REFERENCES public.lessons(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: lessons lessons_module_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.lessons
    ADD CONSTRAINT lessons_module_id_fkey FOREIGN KEY (module_id) REFERENCES public.modules(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: modules modules_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.modules
    ADD CONSTRAINT modules_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: payments payments_course_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT payments_course_id_fkey FOREIGN KEY (course_id) REFERENCES public.courses(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: payments payments_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.payments
    ADD CONSTRAINT payments_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: quiz_results quiz_results_content_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.quiz_results
    ADD CONSTRAINT quiz_results_content_id_fkey FOREIGN KEY (content_id) REFERENCES public.lesson_content(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: quiz_results quiz_results_lesson_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.quiz_results
    ADD CONSTRAINT quiz_results_lesson_id_fkey FOREIGN KEY (lesson_id) REFERENCES public.lessons(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: quiz_results quiz_results_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.quiz_results
    ADD CONSTRAINT quiz_results_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: sessions sessions_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.sessions
    ADD CONSTRAINT sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: student_lesson_access student_lesson_access_created_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.student_lesson_access
    ADD CONSTRAINT student_lesson_access_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: student_lesson_access student_lesson_access_lesson_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.student_lesson_access
    ADD CONSTRAINT student_lesson_access_lesson_id_fkey FOREIGN KEY (lesson_id) REFERENCES public.lessons(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: student_lesson_access student_lesson_access_student_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: langartuser
--

ALTER TABLE ONLY public.student_lesson_access
    ADD CONSTRAINT student_lesson_access_student_id_fkey FOREIGN KEY (student_id) REFERENCES public.profiles(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict jZmKgBTcI1RJG8PCnzJ2vW4ogNd1uurYuZdGRRhy0VIfU6VewhvQC5DH4YzRt7J

