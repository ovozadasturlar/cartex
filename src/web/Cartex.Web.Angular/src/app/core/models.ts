export interface SaleLine {
  saleItemId: number;
  productName: string;
  quantity: number;
  returnedQuantity: number;
  unitPrice: number;
}

export interface SaleDetailLine {
  saleItemId: number;
  variantId: number;
  productName: string;
  returnableQuantity: number;
  unitPrice: number;
}

export interface SaleDetail {
  id: number;
  warehouseId: number;
  customerId: number | null;
  items: SaleDetailLine[];
}

export interface CustomerReturnLine {
  variantId: number;
  saleItemId: number;
  quantity: number;
  reason: string | null;
  condition: string;
  disposition: string;
}

export interface CreateCustomerReturn {
  warehouseId: number;
  lines: CustomerReturnLine[];
  customerId: number | null;
  autoSettle: boolean;
  idempotencyKey: string;
}

export interface CustomerReturnCreated {
  id: number;
  documentNumber: string;
  refundAmount: number;
}

export interface Sale {
  id: number;
  saleDate: string;
  totalAmount: number;
  paidCash: number;
  paidCard: number;
  paidBonus: number;
  debtAmount: number;
  creditAmount: number;
  status: string;
  receiptToken: string;
  customerName: string | null;
  userName: string;
  items: SaleLine[];
}

export interface SalesTotals {
  count: number;
  totalAmount: number;
  totalDiscount: number;
  totalDebt: number;
}

export interface Product {
  id: number;
  defaultVariantId: number;
  name: string;
  categoryName: string | null;
  unitName: string;
  minStock: number;
  barcodes: string[];
  sellingPrice: number | null;
  onHand: number;
  imageUrl: string | null;
  code: string | null;
  priceCurrency: string | null;
}

export interface ProductsTotals {
  count: number;
  totalOnHand: number;
}

export interface CurrencyAmount {
  currency: string;
  amount: number;
}

export interface Customer {
  id: number;
  fullName: string;
  lastName: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  cardBarcode: string | null;
  discountPct: number;
  notificationsOptOut: boolean;
  cashbackBalance: number;
  debtBalance: number;
  creditLimit: number;
  hasTelegram: boolean;
  debtBalances: CurrencyAmount[];
  note?: string | null;
}

export interface CustomerTotals {
  count: number;
  totalDebt: number;
  totalBonus: number;
}

export interface LedgerEntry {
  date: string;
  operationType: string;
  accountType: string;
  change: number;
  balanceAfter: number;
  currency: string | null;
}

export interface TopProduct {
  productId: number;
  productName: string;
  quantity: number;
  revenue: number;
  profit: number;
}

export interface DailySales {
  date: string;
  revenue: number;
  profit: number;
  count: number;
}

export interface SalesReport {
  revenue: number;
  profit: number;
  salesCount: number;
  averageSale: number;
  maxSale: number;
  topProducts: TopProduct[];
  daily: DailySales[];
}

export interface DailyCashFlow {
  date: string;
  income: number;
  expense: number;
  sales: number;
}

export interface DebtAgingRow {
  customerId: number;
  customerName: string;
  balance: number;
  currency: string;
  balanceBase: number;
  lastActivity: string | null;
  daysOverdue: number;
  bucket: string;
}

export interface DebtAgingReport {
  total: number;
  bucket0_30: number;
  bucket31_60: number;
  bucket60Plus: number;
  rows: DebtAgingRow[];
}

export interface CashierSales {
  userId: number;
  userName: string;
  revenue: number;
  count: number;
}

export interface CategorySales {
  categoryName: string | null;
  quantity: number;
  revenue: number;
}

export interface SalesBreakdown {
  cash: number;
  card: number;
  bonus: number;
  debt: number;
  credit: number;
  advance: number;
  returned: number;
  byCashier: CashierSales[];
  byCategory: CategorySales[];
}

export interface CustomerSales {
  customerId: number;
  customerName: string;
  revenue: number;
  profit: number;
  salesCount: number;
  lastPurchase: string;
}

export interface LowStock {
  variantId: number;
  productName: string;
  unitName: string;
  warehouseName: string;
  onHand: number;
  minStock: number;
}

export interface Warehouse {
  id: number;
  name: string;
  branchId: number;
  branchName: string;
  isOnline: boolean;
  assignedUserId: number | null;
}

export interface Receipt {
  receiptToken: string;
  businessName: string;
  branchName: string;
  saleDate: string;
  totalAmount: number;
  discountAmount: number;
  paidCash: number;
  paidCard: number;
  paidBonus: number;
  debtAmount: number;
  creditAmount: number;
  changeAmount: number;
  cashbackEarned: number;
  baseCurrency: string;
  userName: string;
  customerName: string | null;
  items: {
    productName: string;
    quantity: number;
    unitName: string;
    unitPrice: number;
    lineTotal: number;
  }[];
  payments: {
    method: string;
    currency: string;
    amount: number;
    rate: number;
    amountBase: number;
    isForeign: boolean;
  }[];
}

export interface SalePayment {
  method: string;
  currency: string;
  amount: number;
}

