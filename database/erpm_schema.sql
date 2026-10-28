-- =========================================================================================
-- ERPm - HỆ THỐNG QUẢN TRỊ BÁN HÀNG, TỒN KHO & KẾ TOÁN MỞ RỘNG
-- POSTGRESQL PRODUCTION DATABASE SCHEMA & INITIAL SEED SCRIPT
-- Cấu trúc: tbl_{MODULE}_{ENTITY} xoay quanh tâm điểm tbl_SYS_Branch (Chi nhánh)
-- =========================================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. DROP EXISTING TABLES (Reverse Dependency Order - Cả tiền tố mới và tên cũ)
DROP TABLE IF EXISTS "tbl_ACC_JournalEntryLine" CASCADE;
DROP TABLE IF EXISTS "tbl_ACC_JournalEntry" CASCADE;
DROP TABLE IF EXISTS "tbl_SALE_InvoiceItem" CASCADE;
DROP TABLE IF EXISTS "tbl_SALE_Invoice" CASCADE;
DROP TABLE IF EXISTS "tbl_SALE_OrderItem" CASCADE;
DROP TABLE IF EXISTS "tbl_SALE_Order" CASCADE;
DROP TABLE IF EXISTS "tbl_SALES_InvoiceItem" CASCADE;
DROP TABLE IF EXISTS "tbl_SALES_Invoice" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_StockIssueItem" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_StockIssue" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_StockReceiptItem" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_StockReceipt" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_StockBalance" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_Warehouse" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_Item" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_Category" CASCADE;
DROP TABLE IF EXISTS "tbl_WMS_UnitOfMeasure" CASCADE;
DROP TABLE IF EXISTS "tbl_CRM_Partner" CASCADE;
DROP TABLE IF EXISTS "tbl_SYS_Branch" CASCADE;
DROP TABLE IF EXISTS "tbl_SYS_Dictionary" CASCADE;
DROP TABLE IF EXISTS "tbl_SYS_Language" CASCADE;

-- Drop legacy table names if exist
DROP TABLE IF EXISTS journal_entry_lines CASCADE;
DROP TABLE IF EXISTS journal_entries CASCADE;
DROP TABLE IF EXISTS accounts CASCADE;
DROP TABLE IF EXISTS sales_invoice_items CASCADE;
DROP TABLE IF EXISTS sales_invoices CASCADE;
DROP TABLE IF EXISTS stock_issue_items CASCADE;
DROP TABLE IF EXISTS stock_issues CASCADE;
DROP TABLE IF EXISTS stock_receipt_items CASCADE;
DROP TABLE IF EXISTS stock_receipts CASCADE;
DROP TABLE IF EXISTS stock_balances CASCADE;
DROP TABLE IF EXISTS warehouses CASCADE;
DROP TABLE IF EXISTS items CASCADE;
DROP TABLE IF EXISTS categories CASCADE;
DROP TABLE IF EXISTS units_of_measure CASCADE;
DROP TABLE IF EXISTS partners CASCADE;
DROP TABLE IF EXISTS app_dictionaries CASCADE;
DROP TABLE IF EXISTS app_languages CASCADE;

-- =========================================================================================
-- 2. DDL - MODULE SYS: NGÔN NGỮ, TỪ ĐIỂN & CHI NHÁNH CÔNG TY (BRANCH ENGINE)
-- =========================================================================================

CREATE TABLE "tbl_SYS_Language" (
    code VARCHAR(10) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    flag_icon VARCHAR(50),
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INT NOT NULL DEFAULT 0
);

CREATE TABLE "tbl_SYS_Dictionary" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    language_code VARCHAR(10) NOT NULL REFERENCES "tbl_SYS_Language"(code) ON DELETE CASCADE,
    category VARCHAR(50) NOT NULL,
    key VARCHAR(150) NOT NULL,
    value TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ,
    CONSTRAINT uq_lang_dict_key UNIQUE (language_code, key)
);
CREATE INDEX idx_tbl_sys_dict_lookup ON "tbl_SYS_Dictionary"(language_code, category);

CREATE TABLE "tbl_SYS_Branch" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,       -- 'root': Tổng công ty
    name VARCHAR(255) NOT NULL,
    parent_id UUID REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT,
    address TEXT,
    phone VARCHAR(50),
    email VARCHAR(100),
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_sys_branch_code ON "tbl_SYS_Branch"(code);
CREATE INDEX idx_tbl_sys_branch_parent ON "tbl_SYS_Branch"(parent_id);

-- =========================================================================================
-- 3. DDL - MODULE CRM: ĐỐI TÁC, KHÁCH HÀNG & NHÀ CUNG CẤP (PARTNERS)
-- =========================================================================================

CREATE TABLE "tbl_CRM_Partner" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    partner_type INT NOT NULL DEFAULT 1, -- 1: Customer, 2: Supplier, 3: Both
    phone VARCHAR(50),
    email VARCHAR(100),
    address TEXT,
    tax_code VARCHAR(50),
    credit_limit DECIMAL(18, 4) NOT NULL DEFAULT 0,
    debt_balance DECIMAL(18, 4) NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    notes TEXT,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_crm_partner_code ON "tbl_CRM_Partner"(code);
CREATE INDEX idx_tbl_crm_partner_active ON "tbl_CRM_Partner"(is_active) WHERE is_deleted = FALSE;

-- =========================================================================================
-- 4. DDL - MODULE WMS: DANH MỤC HÀNG HÓA & ĐƠN VỊ TÍNH (CATALOG / ITEM)
-- =========================================================================================

CREATE TABLE "tbl_WMS_UnitOfMeasure" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    translations JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);

CREATE TABLE "tbl_WMS_Category" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    translations JSONB NOT NULL DEFAULT '{}'::jsonb,
    parent_id UUID REFERENCES "tbl_WMS_Category"(id) ON DELETE RESTRICT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INT NOT NULL DEFAULT 0,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);

CREATE TABLE "tbl_WMS_Item" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    barcode VARCHAR(100),
    name VARCHAR(255) NOT NULL,
    translations JSONB NOT NULL DEFAULT '{}'::jsonb,
    item_type INT NOT NULL DEFAULT 1, -- 1: Goods, 2: Service, 3: Combo
    category_id UUID REFERENCES "tbl_WMS_Category"(id) ON DELETE SET NULL,
    base_uom_id UUID NOT NULL REFERENCES "tbl_WMS_UnitOfMeasure"(id) ON DELETE RESTRICT,
    sale_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    cost_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    min_stock_alert DECIMAL(18, 4) NOT NULL DEFAULT 0,
    max_stock_alert DECIMAL(18, 4) NOT NULL DEFAULT 0,
    image_url TEXT,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    attributes JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_wms_item_code ON "tbl_WMS_Item"(code);
CREATE INDEX idx_tbl_wms_item_barcode ON "tbl_WMS_Item"(barcode);
CREATE INDEX idx_tbl_wms_item_translations_gin ON "tbl_WMS_Item" USING gin (translations);
CREATE INDEX idx_tbl_wms_item_attributes_gin ON "tbl_WMS_Item" USING gin (attributes);

-- Bảng định nghĩa thuộc tính động của hàng hóa (Schema-Driven Metadata)
CREATE TABLE "tbl_WMS_ItemAttribute" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    category_id UUID REFERENCES "tbl_WMS_Category"(id) ON DELETE CASCADE,
    code VARCHAR(50) NOT NULL,
    name VARCHAR(100) NOT NULL,
    data_type INT NOT NULL DEFAULT 1, -- 1: Text, 2: Number, 3: Select, 4: Date, 5: Boolean
    options JSONB NOT NULL DEFAULT '[]'::jsonb,
    unit VARCHAR(20),
    is_required BOOLEAN NOT NULL DEFAULT FALSE,
    is_filterable BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INT NOT NULL DEFAULT 0,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_wms_item_attribute_category ON "tbl_WMS_ItemAttribute"(category_id);
CREATE INDEX idx_tbl_wms_item_attribute_code ON "tbl_WMS_ItemAttribute"(code);


-- =========================================================================================
-- 5. DDL - MODULE WMS: KHO HÀNG & TỒN KHO TỨC THỜI (INVENTORY & STOCK)
-- =========================================================================================

CREATE TABLE "tbl_WMS_Warehouse" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    translations JSONB NOT NULL DEFAULT '{}'::jsonb,
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT, -- Thuộc Chi nhánh
    address TEXT,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_wms_wh_branch ON "tbl_WMS_Warehouse"(branch_id);

CREATE TABLE "tbl_WMS_StockBalance" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    warehouse_id UUID NOT NULL REFERENCES "tbl_WMS_Warehouse"(id) ON DELETE RESTRICT,
    item_id UUID NOT NULL REFERENCES "tbl_WMS_Item"(id) ON DELETE RESTRICT,
    quantity_on_hand DECIMAL(18, 4) NOT NULL DEFAULT 0,
    average_cost_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID,
    CONSTRAINT uq_tbl_wms_wh_item UNIQUE (warehouse_id, item_id)
);

CREATE TABLE "tbl_WMS_StockReceipt" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    receipt_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT, -- Thuộc Chi nhánh
    warehouse_id UUID NOT NULL REFERENCES "tbl_WMS_Warehouse"(id) ON DELETE RESTRICT,
    supplier_id UUID REFERENCES "tbl_CRM_Partner"(id) ON DELETE SET NULL,
    total_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    status INT NOT NULL DEFAULT 1, -- 0: Draft, 1: Posted, 2: Cancelled
    notes TEXT,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_wms_receipt_branch ON "tbl_WMS_StockReceipt"(branch_id);

CREATE TABLE "tbl_WMS_StockReceiptItem" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stock_receipt_id UUID NOT NULL REFERENCES "tbl_WMS_StockReceipt"(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES "tbl_WMS_Item"(id) ON DELETE RESTRICT,
    quantity DECIMAL(18, 4) NOT NULL DEFAULT 0,
    unit_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    amount DECIMAL(18, 4) NOT NULL DEFAULT 0
);

CREATE TABLE "tbl_WMS_StockIssue" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    issue_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT, -- Thuộc Chi nhánh
    warehouse_id UUID NOT NULL REFERENCES "tbl_WMS_Warehouse"(id) ON DELETE RESTRICT,
    customer_id UUID REFERENCES "tbl_CRM_Partner"(id) ON DELETE SET NULL,
    reason TEXT,
    total_cost_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    status INT NOT NULL DEFAULT 1,
    notes TEXT,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_wms_issue_branch ON "tbl_WMS_StockIssue"(branch_id);

CREATE TABLE "tbl_WMS_StockIssueItem" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stock_issue_id UUID NOT NULL REFERENCES "tbl_WMS_StockIssue"(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES "tbl_WMS_Item"(id) ON DELETE RESTRICT,
    quantity DECIMAL(18, 4) NOT NULL DEFAULT 0,
    unit_cost DECIMAL(18, 4) NOT NULL DEFAULT 0,
    total_cost DECIMAL(18, 4) NOT NULL DEFAULT 0
);

-- =========================================================================================
-- 6. DDL - MODULE ACC: HỆ THỐNG KẾ TOÁN SỔ CÁI BÚT TOÁN KÉP (ACCOUNTING LEDGER)
-- =========================================================================================

CREATE TABLE "tbl_ACC_Account" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    translations JSONB NOT NULL DEFAULT '{}'::jsonb,
    account_type INT NOT NULL, -- 1: Asset, 2: Liability, 3: Equity, 4: Revenue, 5: Expense
    parent_id UUID REFERENCES "tbl_ACC_Account"(id) ON DELETE RESTRICT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);

CREATE TABLE "tbl_ACC_JournalEntry" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    voucher_number VARCHAR(50) NOT NULL UNIQUE,
    voucher_type VARCHAR(50) NOT NULL, -- SALES_INVOICE, STOCK_ISSUE, PAYMENT...
    doc_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    posting_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT, -- Thuộc Chi nhánh
    description TEXT,
    status INT NOT NULL DEFAULT 1,
    reference_id UUID,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_acc_je_posting_date ON "tbl_ACC_JournalEntry"(posting_date);
CREATE INDEX idx_tbl_acc_je_branch ON "tbl_ACC_JournalEntry"(branch_id);

CREATE TABLE "tbl_ACC_JournalEntryLine" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    journal_entry_id UUID NOT NULL REFERENCES "tbl_ACC_JournalEntry"(id) ON DELETE CASCADE,
    account_id UUID REFERENCES "tbl_ACC_Account"(id) ON DELETE RESTRICT,
    debit_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    credit_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    description TEXT,
    partner_id UUID REFERENCES "tbl_CRM_Partner"(id) ON DELETE SET NULL
);
CREATE INDEX idx_tbl_acc_jel_account ON "tbl_ACC_JournalEntryLine"(account_id);

-- =========================================================================================
-- 7. DDL - MODULE SALES: BÁN HÀNG & HÓA ĐƠN POS (SALES & INVOICING)
-- =========================================================================================

CREATE TABLE "tbl_SALE_Invoice" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    invoice_number VARCHAR(50) NOT NULL UNIQUE,
    invoice_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT, -- Đơn thuộc Chi nhánh
    customer_id UUID REFERENCES "tbl_CRM_Partner"(id) ON DELETE SET NULL,
    customer_name VARCHAR(255) NOT NULL DEFAULT 'Khách lẻ',
    warehouse_id UUID NOT NULL REFERENCES "tbl_WMS_Warehouse"(id) ON DELETE RESTRICT,
    payment_method INT NOT NULL DEFAULT 1, -- 1: Cash, 2: Bank, 3: Debt
    status INT NOT NULL DEFAULT 1,
    sub_total DECIMAL(18, 4) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    tax_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    total_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    paid_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    remaining_debt DECIMAL(18, 4) NOT NULL DEFAULT 0,
    journal_entry_id UUID REFERENCES "tbl_ACC_JournalEntry"(id) ON DELETE SET NULL,
    stock_issue_id UUID REFERENCES "tbl_WMS_StockIssue"(id) ON DELETE SET NULL,
    notes TEXT,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_sale_inv_date ON "tbl_SALE_Invoice"(invoice_date);
CREATE INDEX idx_tbl_sale_inv_branch ON "tbl_SALE_Invoice"(branch_id);

CREATE TABLE "tbl_SALE_InvoiceItem" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    sales_invoice_id UUID NOT NULL REFERENCES "tbl_SALE_Invoice"(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES "tbl_WMS_Item"(id) ON DELETE RESTRICT,
    quantity DECIMAL(18, 4) NOT NULL DEFAULT 0,
    unit_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    discount_percent DECIMAL(18, 4) NOT NULL DEFAULT 0,
    amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    cost_price DECIMAL(18, 4) NOT NULL DEFAULT 0
);

-- 7.2. ĐƠN ĐẶT HÀNG BÁN (SALE ORDERS)
CREATE TABLE "tbl_SALE_Order" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_number VARCHAR(50) NOT NULL UNIQUE,
    order_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT,
    customer_id UUID REFERENCES "tbl_CRM_Partner"(id) ON DELETE SET NULL,
    customer_name VARCHAR(255) NOT NULL DEFAULT 'Khách lẻ',
    sub_total DECIMAL(18, 4) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    tax_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    total_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    paid_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    notes TEXT,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_sale_ord_branch ON "tbl_SALE_Order"(branch_id);
CREATE INDEX idx_tbl_sale_ord_date ON "tbl_SALE_Order"(order_date);

CREATE TABLE "tbl_SALE_OrderItem" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    sales_order_id UUID NOT NULL REFERENCES "tbl_SALE_Order"(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES "tbl_WMS_Item"(id) ON DELETE RESTRICT,
    item_code VARCHAR(50) NOT NULL,
    item_name VARCHAR(255) NOT NULL,
    unit_name VARCHAR(50),
    quantity DECIMAL(18, 4) NOT NULL DEFAULT 0,
    unit_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    line_total DECIMAL(18, 4) NOT NULL DEFAULT 0
);

-- =========================================================================================
-- 7.3. DDL - MODULE PURCHASE: QUẢN LÝ MUA HÀNG (PURCHASING)
-- =========================================================================================

CREATE TABLE "tbl_PURCHASE_Order" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_number VARCHAR(50) NOT NULL UNIQUE,
    order_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    branch_id UUID NOT NULL REFERENCES "tbl_SYS_Branch"(id) ON DELETE RESTRICT,
    supplier_id UUID REFERENCES "tbl_CRM_Partner"(id) ON DELETE SET NULL,
    supplier_name VARCHAR(255) NOT NULL,
    sub_total DECIMAL(18, 4) NOT NULL DEFAULT 0,
    tax_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    total_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    paid_amount DECIMAL(18, 4) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    notes TEXT,
    created_at_utc TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(100),
    last_modified_at_utc TIMESTAMPTZ,
    last_modified_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at_utc TIMESTAMPTZ,
    deleted_by VARCHAR(100),
    tenant_id UUID
);
CREATE INDEX idx_tbl_pur_ord_branch ON "tbl_PURCHASE_Order"(branch_id);
CREATE INDEX idx_tbl_pur_ord_date ON "tbl_PURCHASE_Order"(order_date);

CREATE TABLE "tbl_PURCHASE_OrderItem" (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_order_id UUID NOT NULL REFERENCES "tbl_PURCHASE_Order"(id) ON DELETE CASCADE,
    item_id UUID NOT NULL REFERENCES "tbl_WMS_Item"(id) ON DELETE RESTRICT,
    item_code VARCHAR(50) NOT NULL,
    item_name VARCHAR(255) NOT NULL,
    unit_name VARCHAR(50),
    quantity DECIMAL(18, 4) NOT NULL DEFAULT 0,
    unit_price DECIMAL(18, 4) NOT NULL DEFAULT 0,
    line_total DECIMAL(18, 4) NOT NULL DEFAULT 0
);

-- =========================================================================================
-- 8. INITIAL SEED DATA SCRIPT
-- =========================================================================================

-- 8.1. Seed Chi nhánh gốc (Head Office Root Branch)
INSERT INTO "tbl_SYS_Branch" (id, code, name, parent_id, address, phone, email, is_default, is_active)
VALUES 
    ('00000000-0000-0000-0000-000000000001', 'root', 'Tổng Công Ty (Head Office)', NULL, 'Tòa nhà Landmark, Hà Nội', '02439998888', 'contact@erpm.vn', TRUE, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 8.2. Seed Ngôn ngữ giao diện
INSERT INTO "tbl_SYS_Language" (code, name, flag_icon, is_default, is_active, sort_order)
VALUES 
    ('vi', 'Tiếng Việt', 'flag-vn', TRUE, TRUE, 1),
    ('en', 'English', 'flag-us', FALSE, TRUE, 2)
ON CONFLICT (code) DO NOTHING;

-- 8.3. Seed Kho hàng thuộc Chi nhánh Root
INSERT INTO "tbl_WMS_Warehouse" (id, code, name, translations, branch_id, address, is_default, is_active)
VALUES 
    ('00000000-0000-0000-0000-000000000001', 'KHO01', 'Kho Tổng Trung Tâm', '{"vi": "Kho Tổng Trung Tâm", "en": "Main Warehouse"}'::jsonb, '00000000-0000-0000-0000-000000000001', 'Hà Nội, Việt Nam', TRUE, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 8.4. Seed Đơn vị tính
INSERT INTO "tbl_WMS_UnitOfMeasure" (id, code, name, translations, is_active)
VALUES 
    ('00000000-0000-0000-0000-000000000010', 'CAI', 'Cái', '{"vi": "Cái", "en": "Piece"}'::jsonb, TRUE),
    ('00000000-0000-0000-0000-000000000011', 'HOP', 'Hộp', '{"vi": "Hộp", "en": "Box"}'::jsonb, TRUE),
    ('00000000-0000-0000-0000-000000000012', 'RAM', 'Ram', '{"vi": "Ram", "en": "Ream"}'::jsonb, TRUE),
    ('00000000-0000-0000-0000-000000000013', 'KG', 'Kg', '{"vi": "Kilogram", "en": "Kilogram"}'::jsonb, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 8.5. Seed Hệ thống Tài khoản Kế toán (TT200/133)
INSERT INTO "tbl_ACC_Account" (id, code, name, translations, account_type, is_active)
VALUES 
    ('00000000-0000-0000-0000-000000000111', '111', 'Tiền mặt', '{"vi": "Tiền mặt", "en": "Cash on Hand"}'::jsonb, 1, TRUE),
    ('00000000-0000-0000-0000-000000000112', '112', 'Tiền gửi ngân hàng', '{"vi": "Tiền gửi ngân hàng", "en": "Cash in Bank"}'::jsonb, 1, TRUE),
    ('00000000-0000-0000-0000-000000000131', '131', 'Phải thu của khách hàng', '{"vi": "Phải thu của khách hàng", "en": "Accounts Receivable"}'::jsonb, 1, TRUE),
    ('00000000-0000-0000-0000-000000000156', '156', 'Hàng hóa tồn kho', '{"vi": "Hàng hóa tồn kho", "en": "Merchandise Inventory"}'::jsonb, 1, TRUE),
    ('00000000-0000-0000-0000-000000000331', '331', 'Phải trả cho người bán', '{"vi": "Phải trả cho người bán", "en": "Accounts Payable"}'::jsonb, 2, TRUE),
    ('00000000-0000-0000-0000-000000000511', '511', 'Doanh thu bán hàng', '{"vi": "Doanh thu bán hàng", "en": "Sales Revenue"}'::jsonb, 4, TRUE),
    ('00000000-0000-0000-0000-000000000632', '632', 'Giá vốn hàng bán', '{"vi": "Giá vốn hàng bán", "en": "Cost of Goods Sold"}'::jsonb, 5, TRUE),
    ('00000000-0000-0000-0000-000000000642', '642', 'Chi phí quản lý', '{"vi": "Chi phí quản lý", "en": "General & Admin Expenses"}'::jsonb, 5, TRUE),
    ('00000000-0000-0000-0000-000000000911', '911', 'Xác định kết quả kinh doanh', '{"vi": "Xác định kết quả kinh doanh", "en": "Income Summary"}'::jsonb, 3, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 8.6. Seed Khách hàng & Nhà cung cấp
INSERT INTO "tbl_CRM_Partner" (id, code, name, partner_type, phone, email, tax_code, credit_limit, debt_balance, is_active)
VALUES 
    ('00000000-0000-0000-0000-000000001001', 'KH0001', 'Công ty TNHH Minh Khang', 1, '0912345678', 'contact@minhkhang.vn', '0102030405', 20000000, 4500000, TRUE),
    ('00000000-0000-0000-0000-000000001002', 'KH0002', 'Khách Hàng Lẻ Vãng Lai', 1, '0988888888', NULL, NULL, 0, 0, TRUE),
    ('00000000-0000-0000-0000-000000002001', 'NCC0001', 'Tập Đoàn Văn Phòng Phẩm Á Châu', 2, '0243888999', 'supplier@achau.vn', '0304050607', 50000000, -12000000, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 8.7. Seed Danh mục Sản phẩm (ITEM)
INSERT INTO "tbl_WMS_Item" (id, code, barcode, name, translations, item_type, base_uom_id, sale_price, cost_price, min_stock_alert, max_stock_alert, is_active)
VALUES 
    ('00000000-0000-0000-0000-000000003001', 'SP0001', '8935001812345', 'Bút bi Thiên Long 0.5mm', '{"vi": "Bút bi Thiên Long 0.5mm", "en": "Thien Long Ballpoint Pen 0.5mm"}'::jsonb, 1, '00000000-0000-0000-0000-000000000010', 5000, 3200, 20, 500, TRUE),
    ('00000000-0000-0000-0000-000000003002', 'SP0002', '8936002934567', 'Giấy in Double A A4 70gsm', '{"vi": "Giấy in Double A A4 70gsm", "en": "Double A Paper A4 70gsm"}'::jsonb, 1, '00000000-0000-0000-0000-000000000012', 78000, 62000, 10, 200, TRUE),
    ('00000000-0000-0000-0000-000000003003', 'DV0001', NULL, 'Dịch vụ giao hàng tận nơi', '{"vi": "Dịch vụ giao hàng tận nơi", "en": "Door-to-door Delivery Service"}'::jsonb, 2, '00000000-0000-0000-0000-000000000010', 30000, 0, 0, 0, TRUE)
ON CONFLICT (code) DO NOTHING;

-- 8.8. Seed Tồn kho ban đầu
INSERT INTO "tbl_WMS_StockBalance" (warehouse_id, item_id, quantity_on_hand, average_cost_price)
VALUES 
    ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000003001', 150, 3200),
    ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000003002', 45, 62000)
ON CONFLICT (warehouse_id, item_id) DO NOTHING;
