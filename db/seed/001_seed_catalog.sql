SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE Categories;
TRUNCATE TABLE Products;
TRUNCATE TABLE ProductPriceHistory;

INSERT INTO Categories (Id, Name, ParentId, CreatedAt) VALUES
(1, 'CPUs', NULL, UTC_TIMESTAMP(6)),
(2, 'Motherboards', NULL, UTC_TIMESTAMP(6)),
(3, 'Memory', NULL, UTC_TIMESTAMP(6)),
(4, 'Graphics Cards', NULL, UTC_TIMESTAMP(6)),
(5, 'Storage', NULL, UTC_TIMESTAMP(6)),
(6, 'Power Supplies', NULL, UTC_TIMESTAMP(6)),
(7, 'Cases', NULL, UTC_TIMESTAMP(6)),
(8, 'Cooling', NULL, UTC_TIMESTAMP(6)),
(9, 'Peripherals', NULL, UTC_TIMESTAMP(6));

SET FOREIGN_KEY_CHECKS = 1;
