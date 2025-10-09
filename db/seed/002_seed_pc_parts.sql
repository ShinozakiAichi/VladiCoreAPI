SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE Cpus;
TRUNCATE TABLE Motherboards;
TRUNCATE TABLE Rams;
TRUNCATE TABLE Gpus;
TRUNCATE TABLE Psus;
TRUNCATE TABLE Cases;
TRUNCATE TABLE Coolers;
TRUNCATE TABLE Storages;
TRUNCATE TABLE Orders;
TRUNCATE TABLE OrderItems;
TRUNCATE TABLE ProductViews;
TRUNCATE TABLE Products;
TRUNCATE TABLE ProductPriceHistory;

INSERT INTO Cpus (Id, Name, Socket, Tdp, PerfScore) VALUES
(1, 'AMD Ryzen 5 5600X', 'AM4', 65, 320),
(2, 'Intel Core i5-12400F', 'LGA1700', 65, 310),
(3, 'AMD Ryzen 7 5800X3D', 'AM4', 105, 420),
(4, 'Intel Core i7-12700K', 'LGA1700', 125, 450),
(5, 'AMD Ryzen 5 7600', 'AM5', 65, 350);

INSERT INTO Motherboards (Id, Name, Socket, RamType, RamMaxFreq, M2Slots, PcieSlots, FormFactor) VALUES
(1, 'MSI B550 Tomahawk', 'AM4', 'DDR4', 4400, 2, 3, 'ATX'),
(2, 'ASUS PRIME Z690-P', 'LGA1700', 'DDR5', 6000, 3, 4, 'ATX'),
(3, 'Gigabyte B550M DS3H', 'AM4', 'DDR4', 4133, 2, 2, 'mATX'),
(4, 'ASRock B650 Pro RS', 'AM5', 'DDR5', 6400, 3, 4, 'ATX'),
(5, 'ASUS ROG STRIX Z790-F', 'LGA1700', 'DDR5', 7200, 4, 4, 'ATX');

INSERT INTO Rams (Id, Name, Type, Freq, CapacityPerStick, Sticks, PerfScore) VALUES
(1, 'Corsair Vengeance 16GB', 'DDR4', 3200, 8, 2, 250),
(2, 'G.Skill Trident Z 32GB', 'DDR4', 3600, 16, 2, 320),
(3, 'Kingston Fury Beast 32GB', 'DDR5', 5600, 16, 2, 360),
(4, 'Patriot Viper Steel 16GB', 'DDR4', 3600, 8, 2, 280),
(5, 'TeamGroup Delta RGB 32GB', 'DDR5', 6000, 16, 2, 380);

INSERT INTO Gpus (Id, Name, LengthMm, Slots, Tdp, PerfScore) VALUES
(1, 'NVIDIA RTX 4070', 242, 2, 200, 520),
(2, 'AMD Radeon RX 6750 XT', 267, 2, 250, 450),
(3, 'NVIDIA RTX 4060 Ti', 220, 2, 160, 430),
(4, 'AMD Radeon RX 7600', 260, 2, 165, 380),
(5, 'NVIDIA RTX 4090', 304, 3, 450, 700);

INSERT INTO Psus (Id, Name, Wattage, FormFactor) VALUES
(1, 'Corsair RM650x', 650, 'ATX'),
(2, 'Seasonic Focus GX-750', 750, 'ATX'),
(3, 'BeQuiet Pure Power 11 500W', 500, 'ATX'),
(4, 'Corsair SF750', 750, 'SFX'),
(5, 'MSI MPG A850G', 850, 'ATX');

INSERT INTO Cases (Id, Name, GpuMaxLengthMm, CoolerMaxHeightMm, PsuFormFactor) VALUES
(1, 'NZXT H510', 381, 165, 'ATX'),
(2, 'Fractal Meshify 2 Compact', 341, 169, 'ATX'),
(3, 'Cooler Master NR200P', 330, 153, 'SFX'),
(4, 'Lian Li O11 Dynamic', 420, 167, 'ATX'),
(5, 'Phanteks Eclipse G360A', 400, 162, 'ATX');

INSERT INTO Coolers (Id, Name, HeightMm, SocketSupport) VALUES
(1, 'Noctua NH-U12S', 158, '["AM4","LGA1700","AM5"]'),
(2, 'be quiet! Pure Rock 2', 155, '["AM4","LGA1700"]'),
(3, 'Deepcool AK620', 160, '["AM4","LGA1700","AM5"]'),
(4, 'NZXT Kraken X63', 45, '["AM4","LGA1700","AM5"]'),
(5, 'Cooler Master Hyper 212', 159, '["AM4","LGA1700"]');

INSERT INTO Storages (Id, Name, Type, CapacityGb, PerfScore) VALUES
(1, 'Samsung 970 EVO Plus 1TB', 'NVME', 1000, 400),
(2, 'WD Blue SN570 1TB', 'NVME', 1000, 350),
(3, 'Crucial MX500 1TB', 'SATA_SSD', 1000, 280),
(4, 'Seagate Barracuda 2TB', 'HDD', 2000, 120),
(5, 'Samsung 980 Pro 2TB', 'NVME', 2000, 450);

INSERT INTO Products (Id, Sku, Name, CategoryId, Price, OldPrice, Attributes, CreatedAt) VALUES
(1, 'CPU-5600X', 'AMD Ryzen 5 5600X', 1, 249.99, NULL, JSON_OBJECT('componentType','cpu','componentId',1), UTC_TIMESTAMP(6)),
(2, 'CPU-12400F', 'Intel Core i5-12400F', 1, 229.99, NULL, JSON_OBJECT('componentType','cpu','componentId',2), UTC_TIMESTAMP(6)),
(3, 'MB-B550-TOMA', 'MSI B550 Tomahawk', 2, 189.99, NULL, JSON_OBJECT('componentType','motherboard','componentId',1), UTC_TIMESTAMP(6)),
(4, 'MB-Z690P', 'ASUS PRIME Z690-P', 2, 239.99, NULL, JSON_OBJECT('componentType','motherboard','componentId',2), UTC_TIMESTAMP(6)),
(5, 'RAM-32-TZ', 'G.Skill Trident Z 32GB DDR4-3600', 3, 169.99, NULL, JSON_OBJECT('componentType','ram','componentId',2), UTC_TIMESTAMP(6)),
(6, 'GPU-4070', 'NVIDIA RTX 4070', 4, 599.99, NULL, JSON_OBJECT('componentType','gpu','componentId',1), UTC_TIMESTAMP(6)),
(7, 'PSU-GX750', 'Seasonic Focus GX-750', 6, 139.99, NULL, JSON_OBJECT('componentType','psu','componentId',2), UTC_TIMESTAMP(6)),
(8, 'CASE-MESHIFY2C', 'Fractal Meshify 2 Compact', 7, 119.99, NULL, JSON_OBJECT('componentType','case','componentId',2), UTC_TIMESTAMP(6)),
(9, 'COOL-NHU12S', 'Noctua NH-U12S', 8, 69.99, NULL, JSON_OBJECT('componentType','cooler','componentId',1), UTC_TIMESTAMP(6)),
(10, 'SSD-SN570', 'WD Blue SN570 1TB', 5, 89.99, NULL, JSON_OBJECT('componentType','storage','componentId',2), UTC_TIMESTAMP(6)),
(11, 'SSD-MX500', 'Crucial MX500 1TB', 5, 79.99, NULL, JSON_OBJECT('componentType','storage','componentId',3), UTC_TIMESTAMP(6));

INSERT INTO ProductPriceHistory (ProductId, Price, ChangedAt) VALUES
(1, 269.99, UTC_TIMESTAMP(6) - INTERVAL 14 DAY),
(1, 259.99, UTC_TIMESTAMP(6) - INTERVAL 7 DAY),
(1, 249.99, UTC_TIMESTAMP(6) - INTERVAL 1 DAY),
(6, 629.99, UTC_TIMESTAMP(6) - INTERVAL 21 DAY),
(6, 599.99, UTC_TIMESTAMP(6) - INTERVAL 3 DAY);

SET FOREIGN_KEY_CHECKS = 1;
