PRAGMA foreign_keys = ON;

-- =========================
-- 1. CAR CORE
-- =========================

CREATE TABLE CarBrands (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL
);

CREATE TABLE CarModels (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BrandId INTEGER,
    Name TEXT,
    FOREIGN KEY (BrandId) REFERENCES CarBrands(Id)
);

CREATE TABLE CarGenerations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelId INTEGER,
    Name TEXT,
    FOREIGN KEY (ModelId) REFERENCES CarModels(Id)
);

CREATE TABLE CarTrims (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelId INTEGER,
    Name TEXT,
    FOREIGN KEY (ModelId) REFERENCES CarModels(Id)
);

CREATE TABLE CarCategories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT
);

CREATE TABLE Cars (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BrandId INTEGER,
    ModelId INTEGER,
    CategoryId INTEGER,
    Year INTEGER,
    Description TEXT,
    FOREIGN KEY (BrandId) REFERENCES CarBrands(Id),
    FOREIGN KEY (ModelId) REFERENCES CarModels(Id),
    FOREIGN KEY (CategoryId) REFERENCES CarCategories(Id)
);

-- =========================
-- 2. IMAGES & MEDIA
-- =========================

CREATE TABLE CarImages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CarId INTEGER,
    FileName TEXT,
    FilePath TEXT,
    Curid TEXT,
    FOREIGN KEY (CarId) REFERENCES Cars(Id)
);

CREATE TABLE ImageCredits (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CarImageId INTEGER,
    Author TEXT,
    Source TEXT,
    FOREIGN KEY (CarImageId) REFERENCES CarImages(Id)
);

CREATE TABLE ImageLicenses (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT,
    Url TEXT
);

CREATE TABLE ImageTags (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CarImageId INTEGER,
    Tag TEXT,
    FOREIGN KEY (CarImageId) REFERENCES CarImages(Id)
);

CREATE TABLE MediaFiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName TEXT,
    FilePath TEXT,
    Type TEXT
);

CREATE TABLE ImageViews (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CarImageId INTEGER,
    Views INTEGER DEFAULT 0,
    FOREIGN KEY (CarImageId) REFERENCES CarImages(Id)
);

-- =========================
-- 3. USERS & AUTH
-- =========================

CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT,
    Email TEXT,
    PasswordHash TEXT
);

CREATE TABLE Roles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT
);

CREATE TABLE UserRoles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    RoleId INTEGER,
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

CREATE TABLE Sessions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Token TEXT,
    Expiry DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE LoginLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    LoginTime DATETIME,
    IpAddress TEXT,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE PasswordResetTokens (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Token TEXT,
    Expiry DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- =========================
-- 4. ANALYTICS
-- =========================

CREATE TABLE PageViews (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Page TEXT,
    Views INTEGER DEFAULT 0
);

CREATE TABLE CarViews (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CarId INTEGER,
    Views INTEGER DEFAULT 0,
    FOREIGN KEY (CarId) REFERENCES Cars(Id)
);

CREATE TABLE SearchLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Query TEXT,
    SearchTime DATETIME
);

CREATE TABLE TrafficStats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT,
    Visitors INTEGER
);

CREATE TABLE UserActivity (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Activity TEXT,
    Time DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- =========================
-- 5. PAYMENTS
-- =========================

CREATE TABLE Payments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Amount REAL,
    Status TEXT,
    CreatedAt DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE Orders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Total REAL,
    Status TEXT,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE Transactions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PaymentId INTEGER,
    Status TEXT,
    FOREIGN KEY (PaymentId) REFERENCES Payments(Id)
);

CREATE TABLE PaymentMethods (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER,
    Method TEXT,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- =========================
-- 6. SYSTEM
-- =========================

CREATE TABLE Settings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    KeyName TEXT,
    Value TEXT
);

CREATE TABLE SystemLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Log TEXT,
    CreatedAt DATETIME
);