# Veritabanı (SQL Server) Kurulumu

Uygulama **SQL Server** + EF Core kullanır. Aşağıdaki betiği SSMS / Azure Data Studio / `sqlcmd` ile çalıştırın.
Tablo ve kolon adları `Data/AppDbContext.cs` ve `Models/Entities.cs` ile birebir uyumludur; değiştirirseniz modelleri de güncelleyin.

Bağlantı dizesi: `appsettings.json` → `ConnectionStrings:DefaultConnection`
(varsayılan: `(localdb)\MSSQLLocalDB` üzerinde `MarangozDb`).

## 1) Şema

```sql
IF DB_ID(N'MarangozDb') IS NULL CREATE DATABASE MarangozDb;
GO
USE MarangozDb;
GO

-- Kullanıcılar (Role: 'Customer' | 'Manager' | 'Admin')
CREATE TABLE dbo.Users (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    FullName      NVARCHAR(100) NOT NULL,
    Email         NVARCHAR(150) NOT NULL,
    Phone         NVARCHAR(30)  NULL,
    PasswordHash  NVARCHAR(300) NOT NULL,
    [Role]        NVARCHAR(20)  NOT NULL CONSTRAINT DF_Users_Role DEFAULT N'Customer',
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Users_Role CHECK ([Role] IN (N'Customer', N'Manager', N'Admin'))
);
CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users (Email);

CREATE TABLE dbo.Categories (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    [Name]      NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500) NULL
);
CREATE UNIQUE INDEX IX_Categories_Name ON dbo.Categories ([Name]);

CREATE TABLE dbo.Products (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    [Name]          NVARCHAR(150)  NOT NULL,
    [Description]   NVARCHAR(4000) NOT NULL,
    Material        NVARCHAR(100)  NULL,
    Dimensions      NVARCHAR(100)  NULL,
    Price           DECIMAL(18,2)  NULL,
    ImageUrl        NVARCHAR(300)  NULL,
    CategoryId      INT NOT NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    CreatedByUserId INT NULL,
    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id),
    CONSTRAINT FK_Products_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
CREATE INDEX IX_Products_CategoryId ON dbo.Products (CategoryId);
CREATE INDEX IX_Products_CreatedByUserId ON dbo.Products (CreatedByUserId);

-- Teklif / Sipariş talepleri
-- Status: 0=Beklemede, 1=Keşif/Ölçü Alındı, 2=Atölyede Üretimde, 3=Cila/Boya, 4=Tamamlandı, 5=İptal
CREATE TABLE dbo.QuoteRequests (
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_QuoteRequests PRIMARY KEY,
    UserId           INT NOT NULL,
    ProductId        INT NULL,
    Title            NVARCHAR(150)  NOT NULL,
    [Description]    NVARCHAR(4000) NOT NULL,
    CustomDimensions NVARCHAR(200)  NULL,
    Budget           DECIMAL(18,2)  NULL,
    [Status]         INT NOT NULL CONSTRAINT DF_QuoteRequests_Status DEFAULT 0,
    AiCategory       NVARCHAR(50)   NULL,
    AiPriority       NVARCHAR(20)   NULL,
    AiEstimatedDays  INT            NULL,
    AiSummary        NVARCHAR(1000) NULL,
    AiKeywords       NVARCHAR(300)  NULL,
    CreatedAt        DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2 NULL,
    CONSTRAINT CK_QuoteRequests_Status CHECK ([Status] BETWEEN 0 AND 5),
    CONSTRAINT FK_QuoteRequests_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_QuoteRequests_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE SET NULL
);
CREATE INDEX IX_QuoteRequests_UserId ON dbo.QuoteRequests (UserId);
CREATE INDEX IX_QuoteRequests_ProductId ON dbo.QuoteRequests (ProductId);
CREATE INDEX IX_QuoteRequests_Status ON dbo.QuoteRequests ([Status]);

-- Admin log tablosu: kullanıcı silinse bile kayıt korunsun diye UserId üzerinde FK YOKTUR,
-- kullanıcı adı ve rolü anlık görüntü olarak saklanır.
CREATE TABLE dbo.ActivityLogs (
    Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActivityLogs PRIMARY KEY,
    UserId     INT NULL,
    UserName   NVARCHAR(100)  NOT NULL,
    UserRole   NVARCHAR(20)   NOT NULL,
    [Action]   NVARCHAR(50)   NOT NULL,
    EntityType NVARCHAR(50)   NOT NULL,
    EntityId   INT NULL,
    Details    NVARCHAR(1000) NOT NULL,
    CreatedAt  DATETIME2 NOT NULL CONSTRAINT DF_ActivityLogs_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_ActivityLogs_CreatedAt ON dbo.ActivityLogs (CreatedAt);
GO
```

> Not: Zamanlar **UTC** saklanır, arayüzde Türkiye saati (UTC+3) olarak gösterilir.

## 2) Başlangıç verisi (kategoriler)

```sql
USE MarangozDb;
GO
INSERT INTO dbo.Categories ([Name], [Description]) VALUES
 (N'Masa',              N'Yemek, çalışma ve sehpa çeşitleri'),
 (N'Sandalye',          N'Yemek ve çalışma sandalyeleri'),
 (N'Tabure',            N'Bar ve mutfak taburesi çeşitleri'),
 (N'Sallanan Sandalye', N'El yapımı sallanan sandalyeler');
GO
```

## 3) İlk Admin hesabı

Şifre hash'i SQL ile üretilemediği için ilk Admin hesabını uygulama açılışında kendisi oluşturur
(`Data/DbSeeder.cs`): veritabanında hiç `Admin` yoksa `Seed:AdminEmail` / `Seed:AdminPassword` ayarlarını kullanır.

- Geliştirme ortamı için `appsettings.Development.json` içinde örnek değerler vardır
  (`admin@marangoz.local` / `Admin#12345`) — **ilk girişten sonra kendi bilgilerinizle değiştirin.**
- Canlıda şifreyi dosyaya yazmayın; ortam değişkeni veya user-secrets kullanın:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Seed:AdminEmail" "siz@alanadiniz.com"
dotnet user-secrets set "Seed:AdminPassword" "GucluBirSifre!"
```

Yönetici (Manager) hesaplarını Admin olarak giriş yapıp **Panel → Kullanıcılar → Kullanıcı Ekle** ekranından oluşturabilirsiniz.

## 4) Faydalı sorgular

```sql
-- Son 50 log kaydı
SELECT TOP 50 CreatedAt, UserName, UserRole, [Action], Details FROM dbo.ActivityLogs ORDER BY CreatedAt DESC;

-- Duruma göre talep sayıları
SELECT [Status], COUNT(*) AS Adet FROM dbo.QuoteRequests GROUP BY [Status];
```

## 5) Şema güncellemesi (EF Core Migrations)

Şema artık `Migrations/` klasöründeki EF Core migration'larıyla yönetilir. Modeli (`Models/Entities.cs`) değiştirdiğinizde:

```powershell
dotnet ef migrations add DegisiklikAdi
dotnet ef database update
```

- **Sıfırdan kurulum:** Yukarıdaki 1. bölümün SQL'ini çalıştırmak yerine boş bir `MarangozDb` ile `dotnet ef database update` yeterlidir.
- **Bu dosyadaki SQL ile kurulmuş mevcut veritabanı:** Aşağıdaki betik teklif fiyatı/yanıtı, durum geçmişi, teklif fotoğrafları, ürün galerisi, KVKK onayı ve site ayarları tablolarını ekler; ardından `dotnet ef database update` sorunsuz çalışır.

```sql
USE MarangozDb;
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE MigrationId = N'20261120182614_InitialCreate')
    INSERT INTO [__EFMigrationsHistory] VALUES (N'20261120182614_InitialCreate', N'10.0.15');
GO
BEGIN TRANSACTION;
ALTER TABLE [Users] ADD [KvkkAcceptedAt] datetime2 NULL;
ALTER TABLE [QuoteRequests] ADD [CustomerRespondedAt] datetime2 NULL;
ALTER TABLE [QuoteRequests] ADD [CustomerResponse] int NOT NULL DEFAULT 0;
ALTER TABLE [QuoteRequests] ADD [CustomerResponseNote] nvarchar(1000) NULL;
ALTER TABLE [QuoteRequests] ADD [EstimatedDeliveryDate] datetime2 NULL;
ALTER TABLE [QuoteRequests] ADD [QuotedPrice] decimal(18,2) NULL;
ALTER TABLE [QuoteRequests] ADD [WorkshopNote] nvarchar(1000) NULL;

CREATE TABLE [ProductImages] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [Url] nvarchar(300) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
CREATE TABLE [QuoteAttachments] (
    [Id] int NOT NULL IDENTITY,
    [QuoteRequestId] int NOT NULL,
    [Url] nvarchar(300) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_QuoteAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QuoteAttachments_QuoteRequests_QuoteRequestId] FOREIGN KEY ([QuoteRequestId]) REFERENCES [QuoteRequests] ([Id]) ON DELETE CASCADE
);
CREATE TABLE [QuoteStatusHistories] (
    [Id] int NOT NULL IDENTITY,
    [QuoteRequestId] int NOT NULL,
    [Status] int NOT NULL,
    [Note] nvarchar(500) NULL,
    [ImageUrl] nvarchar(300) NULL,
    [ChangedBy] nvarchar(100) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_QuoteStatusHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QuoteStatusHistories_QuoteRequests_QuoteRequestId] FOREIGN KEY ([QuoteRequestId]) REFERENCES [QuoteRequests] ([Id]) ON DELETE CASCADE
);
CREATE TABLE [SiteSettings] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [OrderPhone] nvarchar(30) NULL,
    [Email] nvarchar(150) NULL,
    [Address] nvarchar(300) NULL,
    [WhatsApp] nvarchar(30) NULL,
    [Instagram] nvarchar(200) NULL,
    [MapUrl] nvarchar(500) NULL,
    [WorkingHours] nvarchar(200) NULL,
    CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Id])
);
CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
CREATE INDEX [IX_QuoteAttachments_QuoteRequestId] ON [QuoteAttachments] ([QuoteRequestId]);
CREATE INDEX [IX_QuoteStatusHistories_QuoteRequestId] ON [QuoteStatusHistories] ([QuoteRequestId]);

INSERT INTO [__EFMigrationsHistory] VALUES (N'20261120192943_WorkshopFeatures', N'10.0.15');
COMMIT;
GO
```

## 6) Gizli bilgiler (user-secrets)

Bağlantı dizesindeki SQL şifresini `appsettings.json` içine yazmayın:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=MarangozDb;User Id=KULLANICI;Password=SIFRE;TrustServerCertificate=True;MultipleActiveResultSets=true"
```
