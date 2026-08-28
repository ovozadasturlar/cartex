// Supabase Edge Function: catalog-lookup
// Ochiq o'qish. Do'kon backendi shu yerga murojaat qiladi; do'konda kalit yo'q.
// Baza kaliti faqat shu funksiya ichida, server tomonda ishlatiladi (GKAT-71).
// Ommaviy eksport berilmaydi: bir so'rov bitta mahsulot yoki cheklangan qidiruv.

import { createClient } from "jsr:@supabase/supabase-js@2";

const MAX_SEARCH = 25;
const IMAGE_BASE = Deno.env.get("CATALOG_IMAGE_BASE") ??
  "https://bvkcbcctttqbzvxidlus.supabase.co/storage/v1/object/public/catalog-images/";

const db = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
);

const COLUMNS =
  "barcode, name, name_cyrl, model, unit, pack_qty, image_path, " +
  "catalog_manufacturers(name), catalog_categories(name, parent:parent_id(name))";

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "content-type",
  "Access-Control-Allow-Methods": "GET, OPTIONS",
};

function shape(row: Record<string, unknown>) {
  const category = row.catalog_categories as { name?: string; parent?: { name?: string } } | null;
  const manufacturer = row.catalog_manufacturers as { name?: string } | null;
  return {
    barcode: row.barcode,
    name: row.name,
    nameCyrl: row.name_cyrl,
    manufacturer: manufacturer?.name ?? null,
    categoryParent: category?.parent?.name ?? null,
    categoryChild: category?.name ?? null,
    model: row.model,
    unit: row.unit,
    packQty: row.pack_qty,
    imageUrl: row.image_path ? IMAGE_BASE + String(row.image_path).replace(/^img\//, "") : null,
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...CORS, "content-type": "application/json", "cache-control": "public, max-age=3600" },
  });
}

Deno.serve(async (request) => {
  if (request.method === "OPTIONS") return new Response(null, { headers: CORS });
  if (request.method !== "GET") return json({ error: "method_not_allowed" }, 405);

  const url = new URL(request.url);
  const barcode = url.searchParams.get("barcode")?.trim();
  const query = url.searchParams.get("q")?.trim();

  if (barcode) {
    if (!/^[0-9]{8,14}$/.test(barcode)) return json({ error: "barcode_invalid" }, 400);
    const { data, error } = await db
      .from("catalog_products")
      .select(COLUMNS)
      .eq("status", "published")
      .eq("barcode", barcode)
      .maybeSingle();
    if (error) return json({ error: "lookup_failed" }, 502);
    return json({ found: data !== null, product: data ? shape(data) : null });
  }

  if (query) {
    if (query.length < 3) return json({ error: "query_too_short" }, 400);
    const requested = Math.trunc(Number(url.searchParams.get("limit") ?? 10)) || 10;
    const limit = Math.min(Math.max(requested, 1), MAX_SEARCH);
    const { data, error } = await db
      .from("catalog_products")
      .select(COLUMNS)
      .eq("status", "published")
      .ilike("search_fold", `%${query.toLowerCase()}%`)
      .limit(limit);
    if (error) return json({ error: "lookup_failed" }, 502);
    return json({ items: (data ?? []).map(shape) });
  }

  return json({ error: "barcode_or_q_required" }, 400);
});
