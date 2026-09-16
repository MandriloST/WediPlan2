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
