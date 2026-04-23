-- Trucking Operations Platform — Mock Data
-- Run this after schema.sql.

USE trucking;

INSERT INTO drivers (driver_id, unit_number, first_name, last_name, address, commission_rate) VALUES
(1, '1516', 'John',   'Doe',     '742 Evergreen Terrace, Tuscaloosa, AL', 0.18),
(2, '6690', 'Mike',   'Smith',   '88 Oak Lane, Birmingham, AL',           0.18),
(3, '2234', 'Sarah',  'Johnson', '301 Pine St, Nashville, TN',            0.20),
(4, '3301', 'Carlos', 'Rivera',  '55 River Rd, Memphis, TN',              0.18),
(5, '4450', 'Tommy',  'Hayes',   '190 Cedar Blvd, Jackson, MS',           0.15);

INSERT INTO loads (load_id, load_number, ship_date, origin, destination, description, line_haul_rate, fsc_rate, terms, status, bill_to_name, bill_to_address, consignee_name, consignee_address, driver_id) VALUES
(1,  '1061447', '2026-01-12', 'Birmingham, AL',  'Chicago, IL',      'Steel Coil',    1750.00, 260.00, 'Net 30', 'paid',      'Acme Steel Corp',       '900 Commerce St, Birmingham, AL',  'Chicago Metals Inc',       '500 Industry Ave, Chicago, IL',           1),
(2,  '1061448', '2026-01-16', 'Haleyville, AL',  'Shipshewana, IN',  'Steel Coil',    1143.08,   0.00, 'Net 30', 'paid',      'Bob''s Building Supply','123 First St, Chattanooga, TN',    'Todd Industries',          '456 Second St, Huntsville, AL',           1),
(3,  '1061449', '2026-02-09', 'Memphis, TN',     'Indianapolis, IN', 'Auto Parts',    1578.96, 250.00, 'Net 30', 'invoiced',  'Parts Direct Inc',      '55 Industrial Blvd, Memphis, TN',  'Indy Warehouse LLC',       '800 Cargo Way, Indianapolis, IN',         1),
(4,  '1061450', '2026-01-19', 'Haleyville, AL',  'Shipshewana, IN',  'Steel Coil',    1143.08,   0.00, 'Net 30', 'paid',      'Bob''s Building Supply','123 First St, Chattanooga, TN',    'Todd Industries',          '456 Second St, Huntsville, AL',           2),
(5,  '1061451', '2026-01-25', 'Birmingham, AL',  'Chicago, IL',      'Mixed Freight', 1800.00, 275.00, 'Net 30', 'paid',      'Acme Steel Corp',       '900 Commerce St, Birmingham, AL',  'Chicago Metals Inc',       '500 Industry Ave, Chicago, IL',           2),
(7,  '1061453', '2026-01-22', 'Tuscaloosa, AL',  'Columbus, OH',     'Steel Coil',    1850.00, 280.00, 'Net 30', 'paid',      'Steel Works Inc',       '500 Steel Dr, Tuscaloosa, AL',     'Ohio Metals LLC',          '99 Factory Ln, Columbus, OH',             3),
(8,  '1061454', '2026-01-30', 'Atlanta, GA',     'Detroit, MI',      'Auto Parts',    2200.00, 340.00, 'Net 30', 'paid',      'Atlanta Auto Parts',    '300 Peachtree Rd, Atlanta, GA',    'Detroit Motors',           '400 Assembly Dr, Detroit, MI',            3),
(9,  '1061455', '2026-02-20', 'Nashville, TN',   'Chicago, IL',      'Machinery',     1100.00, 150.00, 'Net 30', 'invoiced',  'TN Machinery Inc',      '88 Industry Ave, Nashville, TN',   'Chicago Mfg Group',        '1200 Lake St, Chicago, IL',               3),
(10, '1061456', '2026-01-28', 'Memphis, TN',     'Indianapolis, IN', 'Auto Parts',    1580.00, 240.00, 'Net 30', 'paid',      'Parts Direct Inc',      '55 Industrial Blvd, Memphis, TN',  'Indy Warehouse LLC',       '800 Cargo Way, Indianapolis, IN',         4),
(11, '1061457', '2026-02-05', 'Jackson, MS',     'Nashville, TN',    'Lumber',         950.00, 130.00, 'Net 30', 'complete',  'Gulf Lumber Co',        '200 Main St, Jackson, MS',         'Nashville Building Supply','77 Mill Rd, Nashville, TN',               4),
(12, '1061458', '2026-01-28', 'Jackson, MS',     'Nashville, TN',    'Paper Products', 950.00, 130.00, 'Net 30', 'paid',      'Gulf Paper Co',         '200 Main St, Jackson, MS',         'Nashville Print Works',    '77 Mill Rd, Nashville, TN',               5),
(13, '1061459', '2026-02-07', 'Huntsville, AL',  'Indianapolis, IN', 'Machinery',     1320.00, 190.00, 'Net 30', 'complete',  'AL Machinery Co',       '14 Factory Rd, Huntsville, AL',    'Indy Mfg Corp',            '30 Warehouse Blvd, Indianapolis, IN',     5),
(14, '1061460', '2026-02-14', 'Jackson, MS',     'Nashville, TN',    'Lumber',        1200.00, 200.00, 'Net 30', 'complete',  'Gulf Lumber Co',        '200 Main St, Jackson, MS',         'Nashville Building Supply','77 Mill Rd, Nashville, TN',               1),
(15, '1061461', '2026-04-07', 'Birmingham, AL',  'Nashville, TN',    'Mixed Freight', 1100.00, 165.00, 'Net 30', 'pending',   'Acme Freight LLC',      '900 Commerce St, Birmingham, AL',  'Nashville Dist Center',    '12 River Rd, Nashville, TN',              1),
(16, '1061462', '2026-04-10', 'Tuscaloosa, AL',  'Columbus, OH',     'Steel Coil',    1850.00, 280.00, 'Net 30', 'pending',   'Steel Works Inc',       '500 Steel Dr, Tuscaloosa, AL',     'Ohio Metals LLC',          '99 Factory Ln, Columbus, OH',             2),
(17, '1061463', '2026-04-15', 'Nashville, TN',   'Louisville, KY',   'Furniture',     1600.00, 243.48, 'Net 30', 'pending',   'Southern Furniture Co', '14 Depot Rd, Tupelo, MS',          'KY Home Supply',           '30 Warehouse Blvd, Louisville, KY',       3),
(18, '1061464', '2026-04-18', 'Memphis, TN',     'Nashville, TN',    'Auto Parts',     880.00, 120.00, 'Net 30', 'pending',   'Parts Direct Inc',      '55 Industrial Blvd, Memphis, TN',  'Nashville Auto Parts',     '200 Garage Way, Nashville, TN',           4),
(19, '1061465', '2026-04-22', 'Jackson, MS',     'Chicago, IL',      'Paper Products',1680.00, 255.00, 'Net 30', 'pending',   'Gulf Paper Co',         '200 Main St, Jackson, MS',         'Chicago Print House',      '400 Press Blvd, Chicago, IL',             5),
(20, '1061466', '2026-02-28', 'Atlanta, GA',     'Detroit, MI',      'Auto Parts',    2200.00,   0.00, 'Net 30', 'cancelled', 'Atlanta Auto Parts',    '300 Peachtree Rd, Atlanta, GA',    'Detroit Motors',           '400 Assembly Dr, Detroit, MI',            1),
(21, '1061467', '2026-03-05', 'Birmingham, AL',  'Nashville, TN',    'Lumber',        1200.00, 185.00, 'Net 30', 'cancelled', 'Acme Freight LLC',      '900 Commerce St, Birmingham, AL',  'Nashville Dist Center',    '12 River Rd, Nashville, TN',              4);

INSERT INTO invoices (invoice_id, invoice_number, load_id, invoice_date, due_date, payment_status, paid_date) VALUES
(1,  '101627', 1,  '2026-01-20', '2026-02-19', 'paid',   '2026-02-12'),
(2,  '101628', 2,  '2026-01-24', '2026-02-23', 'paid',   '2026-02-18'),
(3,  '101629', 4,  '2026-01-27', '2026-02-26', 'paid',   '2026-02-22'),
(4,  '101630', 5,  '2026-02-03', '2026-03-05', 'paid',   '2026-02-28'),
(5,  '101631', 7,  '2026-01-30', '2026-03-01', 'paid',   '2026-02-25'),
(6,  '101632', 8,  '2026-02-06', '2026-03-08', 'paid',   '2026-03-04'),
(7,  '101633', 10, '2026-02-05', '2026-03-07', 'paid',   '2026-03-01'),
(8,  '101634', 12, '2026-02-03', '2026-03-05', 'paid',   '2026-02-27'),
(9,  '101635', 3,  '2026-02-17', '2026-03-19', 'unpaid', NULL),
(11, '101637', 9,  '2026-02-28', '2026-03-30', 'unpaid', NULL);

INSERT INTO driver_advances (advance_id, driver_id, load_id, advance_date, advance_type, amount, notes) VALUES
(1,  1, 1,    '2026-01-12', 'Fuel',      580.00,  'Fuel — Birmingham to Chicago'),
(2,  1, NULL, '2026-01-13', 'Cash',      800.00,  'Weekly cash advance'),
(3,  1, 3,    '2026-02-09', 'Fuel',      650.00,  'Fuel — Memphis to Indianapolis'),
(4,  1, NULL, '2026-02-10', 'EzPass',    127.62,  'Toll charges'),
(5,  1, NULL, '2026-02-13', 'Cash',     1100.00,  'Weekly cash advance'),
(6,  2, 4,    '2026-01-19', 'Fuel',      420.00,  'Fuel — Haleyville run'),
(7,  2, NULL, '2026-01-20', 'Cash',      500.00,  'Weekly cash advance'),
(8,  2, 5,    '2026-01-25', 'Fuel',      580.00,  'Fuel — Birmingham to Chicago'),
(9,  3, 7,    '2026-01-22', 'Fuel',      720.00,  'Fuel — Tuscaloosa to Columbus'),
(10, 3, NULL, '2026-01-22', 'EzPass',     95.00,  'Toll charges'),
(11, 3, NULL, '2026-01-22', 'Insurance', 284.20,  'Monthly insurance deduction'),
(12, 3, 8,    '2026-01-30', 'Fuel',      820.00,  'Fuel — Atlanta to Detroit'),
(13, 4, 10,   '2026-01-28', 'Fuel',      680.00,  'Fuel — Memphis to Indianapolis'),
(14, 5, 12,   '2026-01-28', 'Fuel',      380.00,  'Fuel — Jackson to Nashville'),
(15, 2, 16,   '2026-04-10', 'Fuel',      720.00,  'Fuel — Tuscaloosa to Columbus'),
(16, 3, 17,   '2026-04-15', 'Fuel',      750.00,  'Fuel — Nashville to Louisville'),
(17, 4, NULL, '2026-02-15', 'EzPass',    130.00,  'Toll charges'),
(18, 4, 11,   '2026-02-05', 'Fuel',      450.00,  'Fuel — Jackson to Nashville'),
(19, 5, NULL, '2026-02-10', 'Cash',      350.00,  'Weekly cash advance'),
(20, 5, 13,   '2026-02-07', 'Fuel',      430.00,  'Fuel — Huntsville to Indianapolis');

INSERT INTO driver_pay_summaries (summary_id, driver_id, pay_period_start, pay_period_end, total_line_haul, commission_rate, total_fsc, total_advances, insurance_deduction, workers_comp_deduction, net_pay) VALUES
(1, 1, '2026-01-12', '2026-01-18', 2893.08, 0.18, 260.00, 1380.00, 284.20, 33.70,  934.43),
(2, 1, '2026-02-09', '2026-02-15', 2778.96, 0.18, 450.00, 1877.62, 284.20, 33.70,  533.23),
(3, 2, '2026-01-19', '2026-01-25', 2943.08, 0.18, 275.00, 1500.00, 284.20, 33.70,  870.43),
(4, 3, '2026-01-22', '2026-01-30', 4050.00, 0.20, 620.00, 1635.00, 284.20, 33.70, 1907.10),
(5, 4, '2026-01-26', '2026-02-01', 1580.00, 0.18, 240.00,  680.00, 284.20, 33.70,  537.70),
(6, 5, '2026-01-26', '2026-02-01',  950.00, 0.15, 130.00,  380.00, 284.20, 33.70,  239.60);

-- Users must come last since trucker accounts reference driver_id
-- Trucker passwords match their first name (lowercase) for easy testing
INSERT INTO users (email, phone, password, role, first_name, last_name, driver_id) VALUES
('admin',                  '0000000000', 'admin',    'admin',    'Admin',  'User',    NULL),
('manager@trucking.com',   '2055550100', 'employee', 'employee', 'Jane',   'Manager', NULL),
('john.doe@trucking.com',  '2055550001', 'john',     'trucker',  'John',   'Doe',     1),
('mike.smith@trucking.com','2055550002', 'mike',     'trucker',  'Mike',   'Smith',   2),
('sarah.j@trucking.com',   '2055550003', 'sarah',    'trucker',  'Sarah',  'Johnson', 3),
('carlos.r@trucking.com',  '2055550004', 'carlos',   'trucker',  'Carlos', 'Rivera',  4),
('tommy.h@trucking.com',   '2055550005', 'tommy',    'trucker',  'Tommy',  'Hayes',   5);
