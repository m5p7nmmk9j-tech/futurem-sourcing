-- FUTUREM Sourcing Enterprise
-- MySQL 8 initialization script
-- Core V1.0 schema

CREATE DATABASE IF NOT EXISTS futurem_sourcing
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_unicode_ci;

USE futurem_sourcing;

-- =========================================================
-- Common notes
-- id: internal primary key
-- no: business number, format PREFIX + yyyyMMddHHmmss [+ sequence]
-- is_deleted: soft delete flag
-- =========================================================

CREATE TABLE IF NOT EXISTS roles (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(50) NOT NULL UNIQUE,
  name VARCHAR(100) NOT NULL,
  remark VARCHAR(500) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS users (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  username VARCHAR(80) NOT NULL UNIQUE,
  password_hash VARCHAR(255) NOT NULL,
  display_name VARCHAR(120) NOT NULL,
  email VARCHAR(160) NULL,
  phone VARCHAR(80) NULL,
  status VARCHAR(30) NOT NULL DEFAULT 'active',
  remark VARCHAR(500) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS user_roles (
  user_id BIGINT NOT NULL,
  role_id BIGINT NOT NULL,
  PRIMARY KEY(user_id, role_id),
  CONSTRAINT fk_user_roles_user FOREIGN KEY(user_id) REFERENCES users(id),
  CONSTRAINT fk_user_roles_role FOREIGN KEY(role_id) REFERENCES roles(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS dictionaries (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  dict_type VARCHAR(80) NOT NULL,
  code VARCHAR(80) NOT NULL,
  name VARCHAR(160) NOT NULL,
  sort_no INT NOT NULL DEFAULT 0,
  remark VARCHAR(500) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_dict_type_code(dict_type, code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS markets (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(50) NOT NULL UNIQUE,
  name VARCHAR(160) NOT NULL,
  city VARCHAR(120) NULL,
  address VARCHAR(300) NULL,
  remark VARCHAR(500) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS customers (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(50) NOT NULL UNIQUE,
  name VARCHAR(200) NOT NULL,
  country VARCHAR(120) NULL,
  port VARCHAR(120) NULL,
  contact_name VARCHAR(120) NULL,
  phone VARCHAR(80) NULL,
  whatsapp VARCHAR(80) NULL,
  email VARCHAR(160) NULL,
  currency VARCHAR(20) NOT NULL DEFAULT 'USD',
  credit_limit DECIMAL(18,4) NOT NULL DEFAULT 0,
  credit_days INT NOT NULL DEFAULT 0,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_customers_name(name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS suppliers (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(50) NOT NULL UNIQUE,
  name VARCHAR(200) NOT NULL,
  market_id BIGINT NULL,
  shop_no VARCHAR(80) NULL,
  floor_no VARCHAR(80) NULL,
  booth_no VARCHAR(80) NULL,
  main_products VARCHAR(300) NULL,
  contact_name VARCHAR(120) NULL,
  phone VARCHAR(80) NULL,
  wechat VARCHAR(100) NULL,
  whatsapp VARCHAR(80) NULL,
  email VARCHAR(160) NULL,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_suppliers_name(name),
  KEY idx_suppliers_market(market_id),
  CONSTRAINT fk_suppliers_market FOREIGN KEY(market_id) REFERENCES markets(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS product_categories (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  parent_id BIGINT NULL,
  code VARCHAR(80) NOT NULL UNIQUE,
  name VARCHAR(160) NOT NULL,
  sort_no INT NOT NULL DEFAULT 0,
  remark VARCHAR(500) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_product_categories_parent FOREIGN KEY(parent_id) REFERENCES product_categories(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS products (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  sku VARCHAR(80) NOT NULL UNIQUE,
  barcode VARCHAR(80) NOT NULL UNIQUE,
  name_cn VARCHAR(200) NOT NULL,
  name_en VARCHAR(200) NULL,
  name_es VARCHAR(200) NULL,
  category_id BIGINT NULL,
  brand VARCHAR(120) NULL,
  unit VARCHAR(30) NOT NULL DEFAULT 'PCS',
  customer_item_no VARCHAR(120) NULL,
  image_url VARCHAR(500) NULL,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_products_name(name_cn),
  KEY idx_products_category(category_id),
  CONSTRAINT fk_products_category FOREIGN KEY(category_id) REFERENCES product_categories(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS buying_trips (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  customer_id BIGINT NOT NULL,
  title VARCHAR(200) NULL,
  start_date DATE NULL,
  end_date DATE NULL,
  buyer_user_id BIGINT NULL,
  translator_user_id BIGINT NULL,
  destination_port VARCHAR(120) NULL,
  transport_mode VARCHAR(50) NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_bt_customer(customer_id),
  CONSTRAINT fk_bt_customer FOREIGN KEY(customer_id) REFERENCES customers(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- RFQ
CREATE TABLE IF NOT EXISTS rfqs (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  buying_trip_id BIGINT NULL,
  customer_id BIGINT NOT NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  request_date DATE NULL,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_rfqs_customer(customer_id),
  KEY idx_rfqs_bt(buying_trip_id),
  CONSTRAINT fk_rfqs_customer FOREIGN KEY(customer_id) REFERENCES customers(id),
  CONSTRAINT fk_rfqs_bt FOREIGN KEY(buying_trip_id) REFERENCES buying_trips(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS rfq_items (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  rfq_id BIGINT NOT NULL,
  product_id BIGINT NULL,
  product_name VARCHAR(200) NULL,
  image_url VARCHAR(500) NULL,
  quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
  unit VARCHAR(30) NOT NULL DEFAULT 'PCS',
  specification VARCHAR(500) NULL,
  expected_delivery_date DATE NULL,
  remark VARCHAR(1000) NULL,
  sort_no INT NOT NULL DEFAULT 0,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_rfq_items_rfq(rfq_id),
  CONSTRAINT fk_rfq_items_rfq FOREIGN KEY(rfq_id) REFERENCES rfqs(id),
  CONSTRAINT fk_rfq_items_product FOREIGN KEY(product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS supplier_quotations (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  rfq_item_id BIGINT NOT NULL,
  supplier_id BIGINT NOT NULL,
  purchase_price DECIMAL(18,4) NOT NULL DEFAULT 0,
  currency VARCHAR(20) NOT NULL DEFAULT 'CNY',
  moq DECIMAL(18,4) NOT NULL DEFAULT 0,
  delivery_days INT NOT NULL DEFAULT 0,
  packing_description VARCHAR(500) NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'quoted',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_sq_rfq_item(rfq_item_id),
  KEY idx_sq_supplier(supplier_id),
  CONSTRAINT fk_sq_rfq_item FOREIGN KEY(rfq_item_id) REFERENCES rfq_items(id),
  CONSTRAINT fk_sq_supplier FOREIGN KEY(supplier_id) REFERENCES suppliers(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- CO
CREATE TABLE IF NOT EXISTS customer_orders (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  buying_trip_id BIGINT NULL,
  customer_id BIGINT NOT NULL,
  rfq_id BIGINT NULL,
  order_date DATE NULL,
  currency VARCHAR(20) NOT NULL DEFAULT 'USD',
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_co_customer(customer_id),
  CONSTRAINT fk_co_customer FOREIGN KEY(customer_id) REFERENCES customers(id),
  CONSTRAINT fk_co_bt FOREIGN KEY(buying_trip_id) REFERENCES buying_trips(id),
  CONSTRAINT fk_co_rfq FOREIGN KEY(rfq_id) REFERENCES rfqs(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS customer_order_items (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  customer_order_id BIGINT NOT NULL,
  product_id BIGINT NOT NULL,
  quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
  unit VARCHAR(30) NOT NULL DEFAULT 'PCS',
  sales_price DECIMAL(18,4) NOT NULL DEFAULT 0,
  amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  packing_qty DECIMAL(18,4) NOT NULL DEFAULT 1,
  carton_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_length_cm DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_width_cm DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_height_cm DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_cbm DECIMAL(18,6) NOT NULL DEFAULT 0,
  total_cbm DECIMAL(18,6) NOT NULL DEFAULT 0,
  carton_gw_kg DECIMAL(18,4) NOT NULL DEFAULT 0,
  total_gw_kg DECIMAL(18,4) NOT NULL DEFAULT 0,
  color VARCHAR(120) NULL,
  specification VARCHAR(500) NULL,
  remark VARCHAR(1000) NULL,
  sort_no INT NOT NULL DEFAULT 0,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_co_items_order(customer_order_id),
  KEY idx_co_items_product(product_id),
  CONSTRAINT fk_co_items_order FOREIGN KEY(customer_order_id) REFERENCES customer_orders(id),
  CONSTRAINT fk_co_items_product FOREIGN KEY(product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- PO
CREATE TABLE IF NOT EXISTS purchase_orders (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  buying_trip_id BIGINT NULL,
  customer_order_id BIGINT NULL,
  supplier_id BIGINT NOT NULL,
  customer_id BIGINT NULL,
  order_date DATE NULL,
  expected_delivery_date DATE NULL,
  currency VARCHAR(20) NOT NULL DEFAULT 'CNY',
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  pay_status VARCHAR(50) NOT NULL DEFAULT 'unpaid',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_po_supplier(supplier_id),
  KEY idx_po_customer(customer_id),
  KEY idx_po_co(customer_order_id),
  CONSTRAINT fk_po_supplier FOREIGN KEY(supplier_id) REFERENCES suppliers(id),
  CONSTRAINT fk_po_customer FOREIGN KEY(customer_id) REFERENCES customers(id),
  CONSTRAINT fk_po_co FOREIGN KEY(customer_order_id) REFERENCES customer_orders(id),
  CONSTRAINT fk_po_bt FOREIGN KEY(buying_trip_id) REFERENCES buying_trips(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS purchase_order_items (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  purchase_order_id BIGINT NOT NULL,
  product_id BIGINT NOT NULL,
  quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
  unit VARCHAR(30) NOT NULL DEFAULT 'PCS',
  purchase_price DECIMAL(18,4) NOT NULL DEFAULT 0,
  amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  packing_qty DECIMAL(18,4) NOT NULL DEFAULT 1,
  carton_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_length_cm DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_width_cm DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_height_cm DECIMAL(18,4) NOT NULL DEFAULT 0,
  carton_cbm DECIMAL(18,6) NOT NULL DEFAULT 0,
  total_cbm DECIMAL(18,6) NOT NULL DEFAULT 0,
  carton_gw_kg DECIMAL(18,4) NOT NULL DEFAULT 0,
  total_gw_kg DECIMAL(18,4) NOT NULL DEFAULT 0,
  received_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  color VARCHAR(120) NULL,
  specification VARCHAR(500) NULL,
  remark VARCHAR(1000) NULL,
  sort_no INT NOT NULL DEFAULT 0,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_po_items_order(purchase_order_id),
  KEY idx_po_items_product(product_id),
  CONSTRAINT fk_po_items_order FOREIGN KEY(purchase_order_id) REFERENCES purchase_orders(id),
  CONSTRAINT fk_po_items_product FOREIGN KEY(product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- SO
CREATE TABLE IF NOT EXISTS summary_orders (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  buying_trip_id BIGINT NULL,
  customer_id BIGINT NOT NULL,
  order_date DATE NULL,
  currency VARCHAR(20) NOT NULL DEFAULT 'USD',
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  goods_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  commission_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  warehouse_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  packing_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  loading_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  logistics_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  customs_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  document_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  other_fee DECIMAL(18,4) NOT NULL DEFAULT 0,
  receivable_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  received_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_so_customer(customer_id),
  CONSTRAINT fk_so_customer FOREIGN KEY(customer_id) REFERENCES customers(id),
  CONSTRAINT fk_so_bt FOREIGN KEY(buying_trip_id) REFERENCES buying_trips(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS summary_order_purchase_orders (
  summary_order_id BIGINT NOT NULL,
  purchase_order_id BIGINT NOT NULL,
  PRIMARY KEY(summary_order_id, purchase_order_id),
  CONSTRAINT fk_sopo_so FOREIGN KEY(summary_order_id) REFERENCES summary_orders(id),
  CONSTRAINT fk_sopo_po FOREIGN KEY(purchase_order_id) REFERENCES purchase_orders(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Receiving and QC
CREATE TABLE IF NOT EXISTS receiving_orders (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  purchase_order_id BIGINT NOT NULL,
  receive_date DATE NULL,
  warehouse_location VARCHAR(160) NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_receiving_po(purchase_order_id),
  CONSTRAINT fk_receiving_po FOREIGN KEY(purchase_order_id) REFERENCES purchase_orders(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS receiving_items (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  receiving_order_id BIGINT NOT NULL,
  purchase_order_item_id BIGINT NOT NULL,
  product_id BIGINT NOT NULL,
  received_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  received_carton_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  shortage_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  damaged_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_receiving_items_order FOREIGN KEY(receiving_order_id) REFERENCES receiving_orders(id),
  CONSTRAINT fk_receiving_items_po_item FOREIGN KEY(purchase_order_item_id) REFERENCES purchase_order_items(id),
  CONSTRAINT fk_receiving_items_product FOREIGN KEY(product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS qc_orders (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  purchase_order_id BIGINT NULL,
  receiving_order_id BIGINT NULL,
  qc_date DATE NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  result VARCHAR(50) NOT NULL DEFAULT 'pending',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_qc_po FOREIGN KEY(purchase_order_id) REFERENCES purchase_orders(id),
  CONSTRAINT fk_qc_receiving FOREIGN KEY(receiving_order_id) REFERENCES receiving_orders(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS qc_items (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  qc_order_id BIGINT NOT NULL,
  product_id BIGINT NOT NULL,
  checked_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  passed_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  failed_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  issue_type VARCHAR(100) NULL,
  issue_description VARCHAR(1000) NULL,
  action_result VARCHAR(500) NULL,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_qc_items_order FOREIGN KEY(qc_order_id) REFERENCES qc_orders(id),
  CONSTRAINT fk_qc_items_product FOREIGN KEY(product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Container and Shipment
CREATE TABLE IF NOT EXISTS container_loads (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  summary_order_id BIGINT NULL,
  container_type VARCHAR(50) NOT NULL,
  container_no VARCHAR(120) NULL,
  seal_no VARCHAR(120) NULL,
  load_date DATE NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  total_cbm DECIMAL(18,6) NOT NULL DEFAULT 0,
  total_gw_kg DECIMAL(18,4) NOT NULL DEFAULT 0,
  total_cartons DECIMAL(18,4) NOT NULL DEFAULT 0,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_cl_so FOREIGN KEY(summary_order_id) REFERENCES summary_orders(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS container_load_items (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  container_load_id BIGINT NOT NULL,
  purchase_order_id BIGINT NULL,
  purchase_order_item_id BIGINT NULL,
  product_id BIGINT NOT NULL,
  loaded_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  loaded_carton_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  cbm DECIMAL(18,6) NOT NULL DEFAULT 0,
  gw_kg DECIMAL(18,4) NOT NULL DEFAULT 0,
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_cl_items_cl FOREIGN KEY(container_load_id) REFERENCES container_loads(id),
  CONSTRAINT fk_cl_items_po FOREIGN KEY(purchase_order_id) REFERENCES purchase_orders(id),
  CONSTRAINT fk_cl_items_po_item FOREIGN KEY(purchase_order_item_id) REFERENCES purchase_order_items(id),
  CONSTRAINT fk_cl_items_product FOREIGN KEY(product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS shipments (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  container_load_id BIGINT NULL,
  summary_order_id BIGINT NULL,
  shipment_mode VARCHAR(50) NOT NULL,
  carrier VARCHAR(160) NULL,
  vessel_voyage VARCHAR(160) NULL,
  bill_of_lading_no VARCHAR(160) NULL,
  departure_port VARCHAR(120) NULL,
  destination_port VARCHAR(120) NULL,
  etd DATE NULL,
  eta DATE NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'draft',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_ship_cl FOREIGN KEY(container_load_id) REFERENCES container_loads(id),
  CONSTRAINT fk_ship_so FOREIGN KEY(summary_order_id) REFERENCES summary_orders(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Finance and logs
CREATE TABLE IF NOT EXISTS finance_records (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  no VARCHAR(80) NOT NULL UNIQUE,
  record_type VARCHAR(50) NOT NULL,
  target_type VARCHAR(50) NOT NULL,
  target_id BIGINT NOT NULL,
  customer_id BIGINT NULL,
  supplier_id BIGINT NULL,
  currency VARCHAR(20) NOT NULL DEFAULT 'USD',
  amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  paid_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
  record_date DATE NULL,
  status VARCHAR(50) NOT NULL DEFAULT 'pending',
  remark VARCHAR(1000) NULL,
  is_deleted TINYINT NOT NULL DEFAULT 0,
  created_by BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_by BIGINT NULL,
  updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_fin_target(target_type, target_id),
  KEY idx_fin_customer(customer_id),
  KEY idx_fin_supplier(supplier_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS operation_logs (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  user_id BIGINT NULL,
  action VARCHAR(80) NOT NULL,
  target_type VARCHAR(80) NOT NULL,
  target_id BIGINT NULL,
  target_no VARCHAR(80) NULL,
  before_json JSON NULL,
  after_json JSON NULL,
  ip_address VARCHAR(80) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY idx_logs_target(target_type, target_id),
  KEY idx_logs_user(user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed data
INSERT IGNORE INTO roles(code, name) VALUES
('admin', '管理员'),
('boss', '老板'),
('buyer', '采购员'),
('sales', '业务员'),
('warehouse', '仓库'),
('qc', 'QC'),
('finance', '财务');

INSERT IGNORE INTO markets(code, name, city) VALUES
('YIWU-1', '义乌国际商贸城一区', '义乌'),
('YIWU-2', '义乌国际商贸城二区', '义乌'),
('YIWU-3', '义乌国际商贸城三区', '义乌'),
('YIWU-4', '义乌国际商贸城四区', '义乌'),
('YIWU-5', '义乌国际商贸城五区', '义乌'),
('HUANGYUAN', '篁园市场', '义乌'),
('PRODUCTION-MATERIAL', '国际生产资料市场', '义乌');

INSERT IGNORE INTO dictionaries(dict_type, code, name, sort_no) VALUES
('container_type', '28FT', '28尺柜', 10),
('container_type', '58FT', '58尺柜', 20),
('container_type', '68FT', '68尺柜', 30),
('shipment_mode', 'SEA', '海运', 10),
('shipment_mode', 'AIR', '空运', 20),
('shipment_mode', 'EXPRESS', '快递', 30),
('shipment_mode', 'DIRECT', '直发', 40),
('currency', 'USD', '美元', 10),
('currency', 'CNY', '人民币', 20),
('currency', 'MXN', '墨西哥比索', 30);
