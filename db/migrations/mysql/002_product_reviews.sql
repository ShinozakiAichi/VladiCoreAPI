CREATE TABLE IF NOT EXISTS ProductReviews (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    ProductId INT NOT NULL,
    UserId VARCHAR(64) NULL,
    Rating TINYINT UNSIGNED NOT NULL,
    Title VARCHAR(120) NULL,
    Body TEXT NOT NULL,
    Photos JSON NOT NULL,
    IsApproved TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    CONSTRAINT FK_ProductReviews_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IX_ProductReviews_Product_IsApproved_CreatedAt
    ON ProductReviews (ProductId, IsApproved, CreatedAt DESC);

CREATE TABLE IF NOT EXISTS ProductImages (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    ProductId INT NOT NULL,
    ObjectKey VARCHAR(255) NOT NULL,
    ThumbnailKey VARCHAR(255) NULL,
    Url VARCHAR(512) NOT NULL,
    ThumbnailUrl VARCHAR(512) NULL,
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    CONSTRAINT FK_ProductImages_Product FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX IX_ProductImages_Product_CreatedAt
    ON ProductImages (ProductId, CreatedAt DESC);
