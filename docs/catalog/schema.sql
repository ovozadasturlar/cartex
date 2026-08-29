-- Global katalog sxemasi (Supabase / PostgreSQL).
-- Bu baza faqat bizniki. Do'kon bu bazaga hech qachon ulanmaydi -- unga imzolangan
-- statik paket beriladi. Qoidalar: docs/catalog/catalog-standard.md

create extension if not exists pg_trgm;

-- ---------------------------------------------------------------- ishlab chiqaruvchi
create table catalog_manufacturers (
    id           bigserial primary key,
    name         text        not null,
    name_cyrl    text,
    country      text,
    aliases      text[]      not null default '{}',
    is_active    boolean     not null default true,
    created_at   timestamptz not null default now(),
    updated_at   timestamptz not null default now()
);
create unique index manufacturers_name_key on catalog_manufacturers (lower(name));
create index manufacturers_aliases_idx on catalog_manufacturers using gin (aliases);

-- ---------------------------------------------------------------- kategoriya (2 daraja)
create table catalog_categories (
    id           bigserial primary key,
    parent_id    bigint      references catalog_categories (id) on delete restrict,
    name         text        not null,
    name_cyrl    text,
    sort_order   integer     not null default 0,
    is_active    boolean     not null default true
);
create unique index categories_name_key on catalog_categories (coalesce(parent_id, 0::bigint), lower(name));

create or replace function catalog_categories_depth_guard() returns trigger
language plpgsql as $$
begin
    if new.parent_id is not null
       and exists (select 1 from catalog_categories p
                   where p.id = new.parent_id and p.parent_id is not null) then
        raise exception 'KAT-40: kategoriya faqat ikki darajali bo''ladi';
    end if;
    return new;
end $$;
create trigger catalog_categories_depth before insert or update on catalog_categories
    for each row execute function catalog_categories_depth_guard();

-- ---------------------------------------------------------------- segment

-- ---------------------------------------------------------------- do'kon turi
-- Do'kon turi -- bo'limlar to'plami. Yangi tur qo'shish = bitta qator; mahsulotlar
-- qayta teglanmaydi (GKAT-53).
create table catalog_shop_types (
    code         text primary key,
    name         text   not null,
    name_cyrl    text,
    segments     text[] not null,
    sort_order   integer not null default 0
);

-- ---------------------------------------------------------------- mahsulot
create type catalog_status as enum ('draft', 'review', 'published', 'retired');

create table catalog_products (
    id              bigserial primary key,
    barcode         text              not null,
    name            text              not null,
    name_cyrl       text,
    name_cyrl_override text,
    search_fold     text              not null,
    manufacturer_id bigint            references catalog_manufacturers (id) on delete restrict,
    category_id     bigint            references catalog_categories (id)    on delete restrict,
    model           text,
    unit            text              not null default 'dona',
    pack_qty        numeric(18, 3),
    attributes      jsonb             not null default '{}'::jsonb,
    image_path      text,
    segments        text[]            not null default '{}',
    status          catalog_status    not null default 'draft',
    source          text,
    version         bigint            not null default 1,
    created_at      timestamptz       not null default now(),
    updated_at      timestamptz       not null default now(),

    constraint products_barcode_ean13 check (barcode ~ '^[0-9]{13}$'),
    constraint products_pack_qty_positive check (pack_qty is null or pack_qty > 0)
);

-- KAT-13: katalogda faqat global GS1 kod bo'ladi, shuning uchun barkod butun katalog
-- bo'yicha unikal.
create unique index products_barcode_key on catalog_products (barcode);

create index products_search_idx    on catalog_products using gin (search_fold gin_trgm_ops);
create index products_segments_idx  on catalog_products using gin (segments);
create index products_version_idx   on catalog_products (version) where status = 'published';
create index products_category_idx  on catalog_products (category_id);

-- Chuqur sahifalash: 100 000 qatorda `offset 60000 order by name` 274 ms edi,
-- shu indeks bilan 104 ms. Boshqaruv panelining virtual scroll'i shundan foydalanadi.
create index products_name_idx      on catalog_products (name, id);
create index products_updated_idx   on catalog_products (updated_at, id);

-- ---------------------------------------------------------------- import (staging)

-- ---------------------------------------------------------------- nashr etilgan paketlar

-- ---------------------------------------------------------------- versiya hisoblagichi
create sequence catalog_version_seq;

create or replace function catalog_products_touch() returns trigger
language plpgsql as $$
begin
    new.updated_at := now();
    new.version    := nextval('catalog_version_seq');
    return new;
end $$;
create trigger catalog_products_version before insert or update on catalog_products
    for each row execute function catalog_products_touch();

-- ---------------------------------------------------------------- xavfsizlik
-- KAT-71: hech kim tashqaridan o'qiy ham, yoza ham olmaydi. Yagona chiqish yo'li --
-- bizning asbobimiz service_key bilan yig'adigan imzolangan paket.
alter table catalog_products      enable row level security;
alter table catalog_manufacturers enable row level security;
alter table catalog_categories    enable row level security;
