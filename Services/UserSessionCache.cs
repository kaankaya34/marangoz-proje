using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using web.Data;

namespace web.Services;

// Çerez doğrulamasında her istekte DB'ye gitmemek için kullanıcının güncel rolünü kısa süre önbellekler.
public interface IUserSessionCache
{
    Task<bool> IsValidAsync(int userId, string role);
    void Invalidate(int userId);
}

public class UserSessionCache : IUserSessionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public UserSessionCache(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> IsValidAsync(int userId, string role)
    {
        // Boş string: kullanıcı silinmiş.
        var current = await _cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return await _db.Users.Where(u => u.Id == userId).Select(u => u.Role).FirstOrDefaultAsync() ?? "";
        });
        return current == role;
    }

    public void Invalidate(int userId) => _cache.Remove(Key(userId));

    private static string Key(int userId) => $"user-role:{userId}";
}
