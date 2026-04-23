-- Trucking Operations Platform — Schema
-- Run this file first, then run seed.sql.

CREATE DATABASE IF NOT EXISTS trucking;
USE trucking;

CREATE TABLE IF NOT EXISTS drivers (
    driver_id       INT PRIMARY KEY AUTO_INCREMENT,
    unit_number     VARCHAR(20),
    first_name      VARCHAR(50),
    last_name       VARCHAR(50),
    address         VARCHAR(255),
    commission_rate DECIMAL(5,4) DEFAULT 0.18,
    created_at      DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS loads (
    load_id           INT PRIMARY KEY AUTO_INCREMENT,
    load_number       VARCHAR(50) UNIQUE,
    ship_date         DATE,
    origin            VARCHAR(255),
    destination       VARCHAR(255),
    description       VARCHAR(255),
    line_haul_rate    DECIMAL(10,2),
    fsc_rate          DECIMAL(10,2) DEFAULT 0,
    tarp_rate         DECIMAL(10,2) DEFAULT 0,
    extra_fee         DECIMAL(10,2) DEFAULT 0,
    terms             VARCHAR(50) DEFAULT 'Net 30',
    status            ENUM('pending','complete','invoiced','paid','cancelled') DEFAULT 'pending',
    bill_to_name      VARCHAR(255),
    bill_to_address   VARCHAR(255),
    consignee_name    VARCHAR(255),
    consignee_address VARCHAR(255),
    driver_id         INT,
    created_at        DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (driver_id) REFERENCES drivers(driver_id)
);

CREATE TABLE IF NOT EXISTS invoices (
    invoice_id     INT PRIMARY KEY AUTO_INCREMENT,
    invoice_number VARCHAR(50) UNIQUE,
    load_id        INT,
    invoice_date   DATE,
    due_date       DATE,
    payment_status ENUM('unpaid','paid') DEFAULT 'unpaid',
    paid_date      DATE,
    created_at     DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (load_id) REFERENCES loads(load_id)
);

CREATE TABLE IF NOT EXISTS driver_advances (
    advance_id   INT PRIMARY KEY AUTO_INCREMENT,
    driver_id    INT,
    load_id      INT,
    advance_date DATE,
    advance_type ENUM('Fuel','EzPass','Cash','Insurance','WorkersComp','Other'),
    amount       DECIMAL(10,2),
    notes        VARCHAR(255),
    FOREIGN KEY (driver_id) REFERENCES drivers(driver_id),
    FOREIGN KEY (load_id)   REFERENCES loads(load_id)
);

CREATE TABLE IF NOT EXISTS driver_pay_summaries (
    summary_id             INT PRIMARY KEY AUTO_INCREMENT,
    driver_id              INT,
    pay_period_start       DATE,
    pay_period_end         DATE,
    total_line_haul        DECIMAL(10,2),
    commission_rate        DECIMAL(5,4),
    total_fsc              DECIMAL(10,2),
    total_tarp             DECIMAL(10,2) DEFAULT 0,
    total_extra_fee        DECIMAL(10,2) DEFAULT 0,
    total_advances         DECIMAL(10,2),
    insurance_deduction    DECIMAL(10,2),
    workers_comp_deduction DECIMAL(10,2),
    net_pay                DECIMAL(10,2),
    created_at             DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (driver_id) REFERENCES drivers(driver_id)
);

CREATE TABLE IF NOT EXISTS users (
    user_id    INT PRIMARY KEY AUTO_INCREMENT,
    email      VARCHAR(255) UNIQUE NOT NULL,
    phone      VARCHAR(20)  NOT NULL,
    password   VARCHAR(255) NOT NULL,
    role       ENUM('admin','employee','trucker') NOT NULL,
    first_name VARCHAR(50),
    last_name  VARCHAR(50),
    driver_id  INT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (driver_id) REFERENCES drivers(driver_id)
);

-- Used by the AI chat feature to cache schema embeddings
CREATE TABLE IF NOT EXISTS documents (
    id        INT PRIMARY KEY AUTO_INCREMENT,
    title     VARCHAR(255),
    content   TEXT,
    embedding LONGTEXT
);
