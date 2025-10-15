ALTER TABLE ProductReviews
    ADD COLUMN Status VARCHAR(32) NOT NULL DEFAULT 'Pending',
    ADD COLUMN ModerationNote VARCHAR(500) NULL,
    ADD COLUMN IsDeleted BIT NOT NULL DEFAULT 0,
    ADD COLUMN UsefulUp INT NOT NULL DEFAULT 0,
    ADD COLUMN UsefulDown INT NOT NULL DEFAULT 0;

UPDATE ProductReviews
SET Status = CASE WHEN IsApproved = 1 THEN 'Approved' ELSE 'Pending' END;

ALTER TABLE ProductReviews
    DROP COLUMN IsApproved;

ALTER TABLE ProductReviews
    MODIFY COLUMN Title VARCHAR(140) NULL;

DROP INDEX IX_ProductReviews_Product_IsApproved_CreatedAt ON ProductReviews;

CREATE INDEX IX_ProductReviews_Product_Status_CreatedAt
    ON ProductReviews (ProductId, Status, CreatedAt);

CREATE INDEX IX_ProductReviews_User_Product
    ON ProductReviews (UserId, ProductId);

CREATE TABLE IF NOT EXISTS ProductReviewVotes (
    ReviewId BIGINT NOT NULL,
    UserId CHAR(36) NOT NULL,
    Value TINYINT NOT NULL,
    PRIMARY KEY (ReviewId, UserId),
    CONSTRAINT FK_ProductReviewVotes_Reviews FOREIGN KEY (ReviewId) REFERENCES ProductReviews (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
