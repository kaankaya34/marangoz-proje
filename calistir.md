# Programı Çalıştırma Rehberi

## 1) Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — kontrol: `dotnet --version` (10.x görmelisiniz)
- SQL Server (LocalDB, SQL Express veya tam sürüm) ve SQL çalıştırmak için SSMS / Azure Data Studio / `sqlcmd`

## 2) Veritabanını kurun

1. [db.md](./db.md) dosyasını açın.
2. **1) Şema** ve **2) Başlangıç verisi** bölümlerindeki SQL kodlarını sırayla çalıştırın (`MarangozDb` veritabanı ve tablolar oluşur).
3. Ardından db.md **5) Şema güncellemesi** bölümündeki betiği çalıştırın (teklif fiyatı, galeri, site ayarları vb. tabloları ekler). Alternatif: boş bir `MarangozDb` oluşturup `dotnet ef database update` çalıştırmak 1–3. adımların hepsini tek seferde yapar.

## 3) Bağlantı dizesini ayarlayın

[appsettings.json](./appsettings.json) içindeki `ConnectionStrings:DefaultConnection` değerini kendi SQL Server'ınıza göre düzenleyin.

Varsayılan (LocalDB):

```
Server=(localdb)\\MSSQLLocalDB;Database=MarangozDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

SQL Express kullanıyorsanız:

```
Server=.\\SQLEXPRESS;Database=MarangozDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Aynı dosyada `Site` bölümünden **sipariş telefonunu**, e-postayı ve adresi de değiştirin (`OrderPhone`, `Email`, `Address`).

## 4) Uygulamayı başlatın

Proje klasöründe (`web.csproj` dosyasının olduğu yer) terminal açın:

```powershell
cd C:\Users\abdur\Desktop\web
dotnet run --launch-profile http
```

Tarayıcıda açın: **http://localhost:5178**

Visual Studio / VS Code kullanıyorsanız `F5` ile de başlatabilirsiniz (http profilini seçin).

## 5) İlk giriş (Admin)

Uygulama açılırken veritabanında Admin yoksa otomatik oluşturur. Geliştirme ortamındaki varsayılan hesap
(`appsettings.Development.json`):

| Alan   | Değer                  |
|--------|------------------------|
| E-posta | `admin@marangoz.local` |
| Şifre  | `Admin#12345`          |

Giriş yaptıktan sonra:

1. Sağ üstteki menüden **Yönetim Paneli**'ne girin (`/Panel`).
2. **Kategoriler** ekranında kategorileri kontrol edin (db.md ile 4 tane eklenmişti).
3. **Ürünler → Yeni Ürün** ile ürün ekleyin.
4. **Kullanıcılar → Kullanıcı Ekle** ile Yönetici (Manager) hesapları açın.

> ⚠️ Admin e-posta/şifresini kendi bilgilerinizle değiştirin. Canlı ortamda şifreyi dosyaya yazmayın:
>
> ```powershell
> dotnet user-secrets init
> dotnet user-secrets set "Seed:AdminEmail" "siz@alanadiniz.com"
> dotnet user-secrets set "Seed:AdminPassword" "GucluBirSifre!"
> ```
>
> Admin hesabı bir kez oluştuktan sonra bu ayarlar tekrar kullanılmaz; şifre değiştirmek için Panel'den yeni Admin ekleyip eskisini silebilirsiniz.

## 6) Kullanım özeti

| Rol | Yapabilecekleri |
|-----|-----------------|
| Ziyaretçi | Ürünleri gezer, arar. Sipariş telefonunu göremez. |
| Müşteri | Kayıt/giriş yapar, telefonu görür, teklif talebi oluşturur, **Profil → Taleplerim** sekmesinden durumu rozetlerle izler. |
| Yönetici (Manager) | Panelde ürün/kategori ekler, müşteri ekler/siler, teklif durumunu tek tıkla günceller. |
| Admin | Yöneticinin her şeyi + yönetici/admin ekleme-silme + **Admin Log Sayfası**. |

## 7) Sık karşılaşılan sorunlar

| Sorun | Çözüm |
|-------|-------|
| `Cannot open database "MarangozDb"` / `Invalid object name 'Users'` | db.md SQL'i çalıştırılmamış veya bağlantı dizesi yanlış. |
| Konsolda "Admin hesabı oluşturulmadı" uyarısı | `Seed:AdminEmail` / `Seed:AdminPassword` ayarlanmamış (Production ortamında bu normaldir, user-secrets ile verin). |
| LocalDB bulunamadı | `sqllocaldb start MSSQLLocalDB` çalıştırın veya SQL Express bağlantı dizesini kullanın. |
| Port zaten kullanımda | `Properties\launchSettings.json` içindeki portu değiştirin. |
| Görsel yüklenmiyor | Yalnızca jpg/png/webp ve en fazla 5 MB kabul edilir. |

## 8) Yayına alma (kısaca)

```powershell
dotnet publish -c Release -o publish
```

`publish` klasörünü sunucuya kopyalayın; sunucuda `ASPNETCORE_ENVIRONMENT=Production` kalsın, bağlantı dizesini ve admin bilgilerini ortam değişkeni / user-secrets ile verin. `wwwroot/uploads` klasörü yüklenen ürün görsellerini tutar, yedeğini alın.
