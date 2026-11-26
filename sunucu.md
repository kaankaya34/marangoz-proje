# RESET ETME

# Temizleme ve yeniden yayınlama
rm -rf bin obj web.Tests/bin web.Tests/obj 
dotnet clean
dotnet publish -c Release -o ~/ysmsandalye-yayin
sudo systemctl restart ysmsandalye
# Durum kontrolü
sudo systemctl status ysmsandalye --no-pager -l

# Sunucu Rehberi

Site: https://ysmsandalye.com.tr

```
İnternet → Cloudflare → cloudflared (tünel) → 127.0.0.1:5050 (ysmsandalye) → 127.0.0.1:3306 (MySQL)
```

| Parça | Ne | Nerede |
|-------|----|--------|
| Site | `ysmsandalye.service` | `~/ysmsandalye-yayin` |
| Veritabanı | `mysql.service`, veritabanı `ysmsandalye`, kullanıcı `ysm_app` | phpMyAdmin: `http://<sunucu-ip>/phpmyadmin` |
| Tünel | `cloudflared.service` | token: `/etc/cloudflared/token` |
| Bağlantı ve admin ayarı | `~/ysmsandalye-yayin/appsettings.Production.json` | DB şifresi ve ilk admin burada |

Hepsi sunucu açılınca kendiliğinden başlar.

## Durum kontrolü

```bash
systemctl status ysmsandalye cloudflared mysql
curl -I https://ysmsandalye.com.tr
```

## Restart

```bash
sudo systemctl restart mysql           # veritabanı
sudo systemctl restart cloudflared     # tünel
sudo systemctl restart ysmsandalye     # site
```

Hepsi birden: önce `mysql`, sonra `ysmsandalye`, en son `cloudflared`.

## Veriye bakma

phpMyAdmin'e gir, `ysmsandalye` veritabanını seç. Giriş: kullanıcı `ysm_app` (şifre `appsettings.Production.json` içinde).
Terminalden: `mysql -u ysm_app -p ysmsandalye`

Tablolar: `Users`, `Categories`, `Products`, `ProductImages`, `QuoteRequests`, `QuoteAttachments`, `QuoteStatusHistories`, `ActivityLogs`, `SiteSettings`.

## Yedek

```bash
mysqldump -u ysm_app -p ysmsandalye > ~/ysmsandalye-yedek.sql      # alma
mysql -u ysm_app -p ysmsandalye < ~/ysmsandalye-yedek.sql          # geri yükleme
```

Ürün görselleri veritabanında değil, `~/ysmsandalye-yayin/wwwroot/uploads` altında. Müşteri fotoğrafları (teklif ekleri) `~/ysmsandalye-yayin/App_Data/private-uploads` altında, oturum şifreleme anahtarları `App_Data/keys` altında. Üçünü de yedekle; `App_Data` publish'te silinmez.

## Log

```bash
journalctl -u ysmsandalye -n 50 --no-pager     # site hataları
journalctl -u ysmsandalye -f                   # canlı izle
```

## Siteyi güncelleme (kod değişince)

```bash
cd ~/ysmsandalye-web

git pull // mecbur değil

export PATH=$PATH:$HOME/.dotnet
dotnet publish -c Release -o ~/ysmsandalye-yayin
sudo systemctl restart ysmsandalye
```

Veritabanı şeması değiştiyse restart'tan önce (bağlantıyı ortam değişkeniyle ver):
```bash
export ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;Database=ysmsandalye;User=ysm_app;Password=<şifre>;"
dotnet ef database update
```

`appsettings.Production.json` publish'te silinmez.

## "Bir şeyler ters gitti" hatası

1. `journalctl -u ysmsandalye -n 50 --no-pager` ile hataya bak.
2. `Access denied for user` → DB şifresi `appsettings.Production.json` ile uyuşmuyor.
3. `Unable to connect` → `sudo systemctl status mysql`, çalışmıyorsa `sudo systemctl start mysql`.

## Sunucu sıfırlanırsa

Yedekle: MySQL dökümü (`mysqldump`), `wwwroot/uploads`, `appsettings.Production.json`, cloudflared token'ı (Cloudflare panelinden de alınır).
Sırayla: .NET 10 + MySQL + cloudflared kur → repoyu çek, `dotnet publish` → MySQL'de `ysmsandalye` veritabanını ve `ysm_app` kullanıcısını aç, yedeği geri yükle (yedek yoksa `dotnet ef database update`) → `ysmsandalye.service` dosyasını `/etc/systemd/system/` altına koy, `sudo systemctl enable --now ysmsandalye` → `sudo cloudflared service install <token>`.

> Şifreleri bu dosyaya yazma.
