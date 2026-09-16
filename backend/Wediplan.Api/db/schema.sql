-- REFERENTNA SHEMA Faze 1 (verificirana lokalno u Postgres 16).
-- Izvor istine je EF migracija (`dotnet ef migrations add InitFaza1`) na vašem stroju —
-- ova datoteka služi za pregled/usporedbu i za brzi lokalni test bez .NET-a.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE vendors (
  id                 uuid PRIMARY KEY,
  slug               text NOT NULL,
  name               text NOT NULL,
  category_slug      text NOT NULL,
  region_slug        text NOT NULL,          -- '' za inozemne (country != 'hr')
  country            text NOT NULL DEFAULT 'hr',  -- hr | ba | si
  city               text NOT NULL DEFAULT '',
  lat                double precision,
  lng                double precision,
  location_precision text NOT NULL DEFAULT 'region',
  coverage_regions   text[] NOT NULL DEFAULT '{}',
  coverage_all       boolean NOT NULL DEFAULT false,
  coverage_note      text,
  price_kind         text NOT NULL DEFAULT 'onRequest',
  price_from         integer,
  price_to           integer,
  rating             double precision NOT NULL DEFAULT 0,
  review_count       integer NOT NULL DEFAULT 0,
  rating_source      text,
  verified           boolean NOT NULL DEFAULT false,
  live_calendar      boolean NOT NULL DEFAULT false,
  style_tags         text[] NOT NULL DEFAULT '{}',
  about              text,
  services           text[] NOT NULL DEFAULT '{}',
  website            text,
  phone              text,
  email              text,
  social_instagram   text,
  social_facebook    text,
  claim_status       text NOT NULL DEFAULT 'unclaimed',
  owner_user_id      uuid,
  is_published       boolean NOT NULL DEFAULT true,
  opt_out            boolean NOT NULL DEFAULT false,
  search             tsvector GENERATED ALWAYS AS (
                       to_tsvector('simple',
                         coalesce(name,'') || ' ' || coalesce(city,'') || ' ' || coalesce(about,''))
                     ) STORED,
  created_at         timestamptz NOT NULL DEFAULT now(),
  updated_at         timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ix_vendors_slug ON vendors (slug);
CREATE INDEX ix_vendors_category ON vendors (category_slug);
CREATE INDEX ix_vendors_region ON vendors (region_slug);
CREATE INDEX ix_vendors_search ON vendors USING GIN (search);
CREATE INDEX ix_vendors_name_trgm ON vendors USING GIN (name gin_trgm_ops);
CREATE INDEX ix_vendors_city_trgm ON vendors USING GIN (city gin_trgm_ops);

CREATE TABLE vendor_categories (
  vendor_id     uuid NOT NULL REFERENCES vendors(id) ON DELETE CASCADE,
  category_slug text NOT NULL,
  is_primary    boolean NOT NULL DEFAULT false,
  PRIMARY KEY (vendor_id, category_slug)
);
CREATE INDEX ix_vendor_categories_category ON vendor_categories (category_slug);

CREATE TABLE vendor_photos (
  id          uuid PRIMARY KEY,
  vendor_id   uuid NOT NULL REFERENCES vendors(id) ON DELETE CASCADE,
  storage_key text NOT NULL,
  sort_order  integer NOT NULL DEFAULT 0,
  is_cover    boolean NOT NULL DEFAULT false
);
CREATE INDEX ix_vendor_photos_vendor ON vendor_photos (vendor_id);

CREATE TABLE imported_reviews (
  id        uuid PRIMARY KEY,
  vendor_id uuid NOT NULL REFERENCES vendors(id) ON DELETE CASCADE,
  author    text NOT NULL,
  rating    integer NOT NULL,
  text      text NOT NULL,
  source    text NOT NULL,
  year      integer NOT NULL
);
CREATE INDEX ix_imported_reviews_vendor ON imported_reviews (vendor_id);

CREATE TABLE events (
  id           bigserial PRIMARY KEY,
  ts           timestamptz NOT NULL DEFAULT now(),
  event_name   text NOT NULL,
  session_hash text,
  page         text,
  props        jsonb
);
CREATE INDEX ix_events_ts ON events (ts);
CREATE INDEX ix_events_name_ts ON events (event_name, ts);

CREATE TABLE daily_stats (
  day           date NOT NULL,
  event_name    text NOT NULL,
  category_slug text NOT NULL DEFAULT '',
  region_slug   text NOT NULL DEFAULT '',
  vendor_slug   text NOT NULL DEFAULT '',
  count         integer NOT NULL DEFAULT 0,
  PRIMARY KEY (day, event_name, category_slug, region_slug, vendor_slug)
);

CREATE TABLE sponsorships (
  id         uuid PRIMARY KEY,
  vendor_id  uuid NOT NULL,
  kind       text NOT NULL,
  scope      text,
  starts_at  timestamptz,
  ends_at    timestamptz
);
CREATE INDEX ix_sponsorships_vendor ON sponsorships (vendor_id);

-- ============================================================================
-- Faza 3 — auth (ASP.NET Core Identity nad Guid + pomoćne tablice).
-- REFERENCA: pravu shemu generira `dotnet ef migrations add Faza3Auth`. Identity
-- tablice (users, roles, user_roles…) EF stvara automatski; ovdje su radi pregleda i
-- brzog testa (psql -f). Standardna Identity shema, samo u snake_case imenima (v. AppDbContext).
-- ============================================================================

CREATE TABLE users (
  id                     uuid PRIMARY KEY,
  user_name              varchar(256),
  normalized_user_name   varchar(256),
  email                  varchar(256),
  normalized_email       varchar(256),
  email_confirmed        boolean NOT NULL DEFAULT false,
  password_hash          text,
  security_stamp         text,
  concurrency_stamp      text,
  phone_number           text,
  phone_number_confirmed boolean NOT NULL DEFAULT false,
  two_factor_enabled     boolean NOT NULL DEFAULT false,
  lockout_end            timestamptz,
  lockout_enabled        boolean NOT NULL DEFAULT false,
  access_failed_count    integer NOT NULL DEFAULT 0,
  -- naša dodatna polja (AppUser)
  display_name           text,
  created_at             timestamptz NOT NULL DEFAULT now(),
  last_login_at          timestamptz
);
CREATE UNIQUE INDEX ix_users_normalized_email ON users (normalized_email);
CREATE UNIQUE INDEX ix_users_normalized_user_name ON users (normalized_user_name);

CREATE TABLE roles (
  id                uuid PRIMARY KEY,
  name              varchar(256),
  normalized_name   varchar(256),
  concurrency_stamp text
);
CREATE UNIQUE INDEX ix_roles_normalized_name ON roles (normalized_name);

CREATE TABLE user_roles (
  user_id uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
  role_id uuid NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
  PRIMARY KEY (user_id, role_id)
);
CREATE INDEX ix_user_roles_role_id ON user_roles (role_id);

CREATE TABLE user_claims (
  id         serial PRIMARY KEY,
  user_id    uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
  claim_type text,
  claim_value text
);
CREATE INDEX ix_user_claims_user_id ON user_claims (user_id);

CREATE TABLE user_logins (
  login_provider        text NOT NULL,
  provider_key          text NOT NULL,
  provider_display_name text,
  user_id               uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
  PRIMARY KEY (login_provider, provider_key)
);
CREATE INDEX ix_user_logins_user_id ON user_logins (user_id);

CREATE TABLE user_tokens (
  user_id        uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
  login_provider text NOT NULL,
  name           text NOT NULL,
  value          text,
  PRIMARY KEY (user_id, login_provider, name)
);

CREATE TABLE role_claims (
  id          serial PRIMARY KEY,
  role_id     uuid NOT NULL REFERENCES roles (id) ON DELETE CASCADE,
  claim_type  text,
  claim_value text
);
CREATE INDEX ix_role_claims_role_id ON role_claims (role_id);

-- pomoćne auth tablice (naše)
CREATE TABLE magic_links (
  id          uuid PRIMARY KEY,
  email       varchar(320) NOT NULL,
  token_hash  text NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  expires_at  timestamptz NOT NULL,
  consumed_at timestamptz,
  request_ip  text
);
CREATE UNIQUE INDEX ix_magic_links_token_hash ON magic_links (token_hash);
CREATE INDEX ix_magic_links_email ON magic_links (email);

CREATE TABLE email_verification_tokens (
  id          uuid PRIMARY KEY,
  user_id     uuid NOT NULL,
  token_hash  text NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  expires_at  timestamptz NOT NULL,
  consumed_at timestamptz
);
CREATE UNIQUE INDEX ix_email_verification_tokens_token_hash ON email_verification_tokens (token_hash);
CREATE INDEX ix_email_verification_tokens_user_id ON email_verification_tokens (user_id);
