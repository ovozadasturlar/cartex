-- Pul va qoldiq invariantlari. Bazaga qarshi to'g'ridan-to'g'ri ishlatiladi:
--   docker exec <postgres> psql -U postgres -d cartex_db -f /dev/stdin < scripts/pul-invariantlari.sql
-- Har qator "buzilgan" ustunida 0 bo'lishi shart. Noldan farq — hisob-kitobda yoriq bor.

\echo '== 1. Savdo to\lov invarianti: naqd+karta+bonus+avans+qarz = jami+haqdorlik =='
select count(*) as savdolar,
       count(*) filter (where paid_cash + paid_card + paid_bonus + paid_advance + debt_amount
                              <> total_amount + credit_amount) as buzilgan
from sales where is_deleted = false;

\echo '== 2. Savdo qatorlari sarlavhaga mos: sum(miqdor*narx) = jami + chegirma =='
select count(*) as savdolar,
       count(*) filter (where abs(x.hisob - (s.total_amount + s.discount_amount)) > 0.02) as buzilgan
from sales s
join lateral (select coalesce(sum(i.quantity * i.unit_price), 0) as hisob
              from sale_items i where i.sale_id = s.id) x on true
where s.is_deleted = false and s.status <> 'Voided';

\echo '== 3. Hisob qoldiqlari daftar harakatlariga mos (ikki tomonlama yozuv) =='
with harakat as (
  select a.id, a.balance,
         coalesce((select sum(t.amount) from transactions t where t.to_account_id = a.id), 0)
       - coalesce((select sum(t.amount) from transactions t where t.from_account_id = a.id), 0) as hisoblangan
  from accounts a where a.is_deleted = false
)
select count(*) as hisoblar, count(*) filter (where abs(balance - hisoblangan) > 0.005) as buzilgan
from harakat;

-- Daftar mantiqi: mijoz qarzi musbat (u bizga qarzdor), taminotchi qarzi manfiy (biz unga).
\echo '== 4. Qarz hisoblari yonalishi togri =='
select count(*) as qarz_hisoblari,
       count(*) filter (where customer_id is not null and balance < -0.005) as mijoz_buzilgan,
       count(*) filter (where supplier_id is not null and balance > 0.005) as taminotchi_buzilgan
from accounts where is_deleted = false and type = 'Debt';

\echo '== 5. Qaytarilgan summa savdodan oshmaydi =='
select count(*) as savdolar,
       count(*) filter (where refunded_cash + refunded_card + refunded_bonus + refunded_debt + refunded_advance
                              > total_amount + 0.02) as buzilgan
from sales where is_deleted = false;

\echo '== 6. Qoldiq partiyalari: defitsit bo\lmagan qator manfiy emas =='
select count(*) as partiyalar, count(*) filter (where quantity < -0.001 and is_deficit = false) as buzilgan
from stocks where is_deleted = false;

\echo '== 7. Yopilgan smenada sanalgan naqd yozilgan (farq biznes fakti, xato emas) =='
select count(*) as yopilgan, count(*) filter (where counted_cash is null) as buzilgan
from shifts where is_deleted = false and status = 'Closed';

\echo '== 8. Har savdo hujjatida chek belgisi bor va takrorlanmaydi =='
select count(*) as savdolar,
       count(*) filter (where receipt_token is null or receipt_token = '') as belgisiz,
       count(*) - count(distinct receipt_token) as takroriy
from sales where is_deleted = false;

\echo '== 9. Idempotentlik: bir xil kalit bilan ikkinchi savdo yaratilmagan =='
select coalesce(sum(n - 1), 0) as buzilgan
from (select idempotency_key, count(*) n from sales
      where is_deleted = false and idempotency_key is not null group by 1) x;

\echo '== 10. Oflayn hodisalar takrorlanmagan (EventId bo\yicha) =='
select count(*) as hodisalar, count(*) - count(distinct event_id) as buzilgan
from offline_sync_events;

\echo '== 11. Qaytarilgan savdoda haqiqiy qaytarish hujjati bor va summasi mos =='
select count(*) as qaytarilgan,
       count(*) filter (where doc_refund is null) as hujjatsiz,
       count(*) filter (where doc_refund is not null and abs(doc_refund - s.refunded_cash) > 0.01) as summa_farqli
from sales s
left join lateral (
    select sum(d.refund_amount) as doc_refund
    from customer_return_lines l
    join customer_return_documents d on d.id = l.customer_return_document_id
    where l.sale_id = s.id
) r on true
where s.is_deleted = false and s.refunded_cash > 0;
