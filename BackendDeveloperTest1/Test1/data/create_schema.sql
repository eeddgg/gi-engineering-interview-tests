-- SQLite
drop table "member";
drop table "account";
drop table "location";



CREATE TABLE "location" (
  "UID" integer NOT NULL PRIMARY KEY AUTOINCREMENT,
  "Guid" char(36) NOT NULL,
  "CreatedUtc" datetime NOT NULL,
  "UpdatedUtc" datetime DEFAULT NULL,
  "Name" varchar(45) NOT NULL,
  "Disabled" tinyint NOT NULL,
  "EnableBilling" tinyint NOT NULL,
  "AccountStatus" int NOT NULL,
  "Address" varchar(45) DEFAULT NULL,
  "City" varchar(45) DEFAULT NULL,
  "Locale" varchar(45) DEFAULT NULL,
  "PostalCode" varchar(16) DEFAULT NULL
);

CREATE TABLE "account" (
  "UID" integer NOT NULL PRIMARY KEY AUTOINCREMENT,
  "LocationUid" int unsigned NOT NULL,
  "Guid" char(36) NOT NULL,
  "CreatedUtc" datetime NOT NULL,
  "UpdatedUtc" datetime DEFAULT NULL,
  "Status" int unsigned NOT NULL,
  "EndDateUtc" datetime DEFAULT NULL,
  "AccountType" int NOT NULL,
  "PaymentAmount" double DEFAULT NULL,
  "PendCancel" tinyint NOT NULL,
  "PendCancelDateUtc" datetime DEFAULT NULL,
  "PeriodStartUtc" datetime NOT NULL,
  "PeriodEndUtc" datetime NOT NULL,
  "NextBillingUtc" datetime NOT NULL,
  FOREIGN KEY("LocationUid") REFERENCES location("UID")
);

CREATE TABLE "member" (
  "UID" integer NOT NULL PRIMARY KEY AUTOINCREMENT,
  "Guid" char(36) NOT NULL,
  "AccountUid" int unsigned NOT NULL,
  "LocationUid" int unsigned NOT NULL,
  "CreatedUtc" datetime NOT NULL,
  "UpdatedUtc" datetime DEFAULT NULL,
  -- Why are we using a reserved keyword in SQLite as a column identifier? This just creates errors
  "Primary" tinyint NOT NULL,
  "JoinedDateUtc" datetime NOT NULL,
  "CancelDateUtc" datetime DEFAULT NULL,
  "FirstName" varchar(45),
  "LastName" varchar(45),
  "Address" varchar(45),
  "City" varchar(45),
  "Locale" varchar(16),
  "PostalCode" varchar(16),
  "Cancelled" tinyint NOT NULL,
  FOREIGN KEY("LocationUid") REFERENCES location("UID"),
  FOREIGN KEY("AccountUid") REFERENCES account("UID")
);
