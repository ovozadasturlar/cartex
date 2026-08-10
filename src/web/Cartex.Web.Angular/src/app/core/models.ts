export interface SaleLine {
  saleItemId: number;
  productName: string;
  quantity: number;
  returnedQuantity: number;
  unitPrice: number;
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

export interface TradeCaseLine {
  id: number;
  goodsIssueLineId: number;
  goodsIssueDocumentId: number;
  issueDocumentNumber: string;
  issueDate: string;
  variantId: number;
  productName: string;
  unitName: string;
  issuedQuantity: number;
  returnedQuantity: number;
  settledQuantity: number;
  custodyQuantity: number;
  unitPrice: number;
  priceCurrency: string;
  priceRate: number;
  barcode: string | null;
}

export interface TradeCaseAllowedActions {
  canEdit: boolean;
  canIssue: boolean;
  canReturn: boolean;
  canSettle: boolean;
  canCancel: boolean;
  canReceivePayment: boolean;
  canExportStatement: boolean;
}

export interface TradeCaseParticipant {
  roleDefinitionId: number;
  partyId: number;
  roleLabel: string;
  partyName: string;
  partyPhone: string | null;
}

export interface TradeCaseDocument {
  type: string;
  id: number;
  documentNumber: string;
  businessDate: string;
  createdAt: string;
  status: string;
  quantity: number;
  amount: number;
  note: string | null;
  saleId: number | null;
  receiptToken: string | null;
}

export interface TradeCaseList {
  id: number;
  caseNumber: string;
  businessDate: string;
  title: string;
  customerId: number;
  customerName: string;
  warehouseId: number;
  warehouseName: string;
  workflow: string;
  pricePolicy: string;
  status: string;
  issuedQuantity: number;
  returnedQuantity: number;
  settledQuantity: number;
  custodyQuantity: number;
  estimatedOutstandingAmount: number;
  currency: string;
  version: number;
  updatedAt: string;
}

export interface TradeCaseDetail extends TradeCaseList {
  siteAddress: string | null;
  branchId: number;
  branchName: string;
  customerPhone: string | null;
  customerName: string;
  note: string | null;
  createdAt: string;
  updatedAt: string;
  lines: TradeCaseLine[];
  documents: TradeCaseDocument[];
  allowedActions: TradeCaseAllowedActions;
  participants: TradeCaseParticipant[];
}

export interface TradeCaseStatementEntry {
  occurredAt: string;
  type: string;
  documentId: number;
  documentNumber: string;
  summary: string;
  debit: number;
  credit: number;
  runningBalance: number;
  currency: string;
  saleId: number | null;
  receiptToken: string | null;
}

export interface TradeCaseStatementProduct {
  variantId: number;
  productName: string;
  unitName: string;
  issued: number;
  returnedSellable: number;
  returnedNonSellable: number;
  settled: number;
  outstandingCustody: number;
  averageUnitPrice: number;
  chargedAmount: number;
}

export interface TradeCaseStatement {
  tradeCaseId: number;
  caseNumber: string;
  caseTitle: string;
  customerId: number;
  customerName: string;
  from: string | null;
  to: string | null;
  currency: string;
  openingBalance: number;
  closingBalance: number;
  timeline: TradeCaseStatementEntry[];
  products: TradeCaseStatementProduct[];
  generatedAt: string;
}

export interface GoodsIssuePrintLine {
  productName: string;
  unitShortName: string;
  barcode: string | null;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface GoodsIssuePrint {
  issueId: number;
  documentNumber: string;
  businessDate: string;
  createdAt: string;
  tradeCaseId: number;
  caseNumber: string;
  caseTitle: string;
  customerName: string;
  customerPhone: string | null;
  branchName: string;
  warehouseName: string;
  sellerName: string;
  note: string | null;
  currency: string;
  totalAmount: number;
  lines: GoodsIssuePrintLine[];
}

export interface CreateTradeCaseRequest {
  customerId: number;
  warehouseId: number;
  title: string;
  siteAddress: string;
  workflow: 'CustodyUntilSettlement' | 'CustodyWithoutSettlement';
  pricePolicy: 'SnapshotAtIssue' | 'Settlement';
  currency: string;
  businessDate: string;
  note: string | null;
  idempotencyKey: string;
  participants: { roleDefinitionId: number; partyId: number }[] | null;
}

export interface UpdateTradeCaseRequest {
  title: string;
  siteAddress: string;
  note: string | null;
  participants: { roleDefinitionId: number; partyId: number }[] | null;
  expectedVersion: number;
}

export interface ChangeTradeCaseStatusRequest {
  reason: string | null;
  expectedVersion: number;
}

export interface CreateGoodsIssueRequest {
  lines: { variantId: number; quantity: number; unitPrice: number }[];
  businessDate: string;
  note: string;
  idempotencyKey: string;
  expectedCaseVersion: number;
}

export interface GoodsIssueCreated {
  id: number;
  documentNumber: string;
  estimatedAmount: number;
  caseVersion: number;
}

export interface GoodsReturnLineRequest {
  goodsIssueLineId: number;
  quantity: number;
  reason: string | null;
  condition: string;
  disposition: string;
}

export interface CreateGoodsReturnRequest {
  lines: GoodsReturnLineRequest[];
  businessDate: string;
  note: string;
  idempotencyKey: string;
  expectedCaseVersion: number;
}

export interface GoodsReturnCreated {
  id: number;
  documentNumber: string;
  caseVersion: number;
}

export interface TradeCaseSettlementLineRequest {
  goodsIssueLineId: number;
  quantity: number;
}

export interface SalePayment {
  method: string;
  currency: string;
  amount: number;
}

export interface SettleTradeCaseRequest {
  paidCash: number;
  paidCard: number;
  paidBonus: number;
  payments: SalePayment[] | null;
  lines: TradeCaseSettlementLineRequest[] | null;
  discountAmount: number;
  debtCurrency: string | null;
  debtDueDate: string | null;
  applyAutoDiscount: boolean;
  useCustomerAdvance: boolean;
  closeWhenEmpty: boolean;
  businessDate: string;
  note: string | null;
  idempotencyKey: string;
  expectedCaseVersion: number;
}

export interface TradeCaseSettlementCreated {
  id: number;
  documentNumber: string;
  saleId: number;
  receiptToken: string;
  amount: number;
  caseSettled: boolean;
  caseVersion: number;
}
