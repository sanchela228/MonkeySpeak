using Core.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.Database.Services;

public class FriendshipService
{
    private readonly Context _context;

    public FriendshipService(Context context)
    {
        _context = context;
    }

    public async Task<Friendship> SendFriendRequestAsync(Guid fromUserId, Guid toUserId)
    {
        if (fromUserId == toUserId)
        {
            throw new InvalidOperationException("Cannot send friend request to yourself");
        }

        var existingFriendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                (f.UserId == fromUserId && f.FriendId == toUserId) ||
                (f.UserId == toUserId && f.FriendId == fromUserId));

        if (existingFriendship != null)
        {
            throw new InvalidOperationException("Friendship already exists");
        }

        var friendship = new Friendship
        {
            UserId = fromUserId,
            FriendId = toUserId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        return friendship;
    }

    public async Task<Friendship> AcceptFriendRequestAsync(Guid friendshipId, Guid acceptingUserId)
    {
        var friendship = await _context.Friendships.FindAsync(friendshipId);
        if (friendship == null)
        {
            throw new InvalidOperationException("Friendship not found");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            throw new InvalidOperationException("Friendship is not pending");
        }

        // SECURITY: Only the recipient can accept the friend request
        if (friendship.FriendId != acceptingUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to accept this friend request");
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.AcceptedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return friendship;
    }

    public async Task RemoveFriendAsync(Guid userId, Guid friendId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                (f.UserId == userId && f.FriendId == friendId) ||
                (f.UserId == friendId && f.FriendId == userId));

        if (friendship != null)
        {
            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveFriendshipByIdAsync(Guid friendshipId, Guid userId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.Id == friendshipId);

        if (friendship == null)
            throw new Exception("Friendship not found");

        // Validate that the user is part of this friendship (security check)
        if (friendship.UserId != userId && friendship.FriendId != userId)
            throw new Exception("Not authorized to remove this friendship");

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
    }

    public Task<Friendship?> GetFriendshipByIdAsync(Guid friendshipId)
    {
        return _context.Friendships
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(f => f.Id == friendshipId);
    }

    public async Task<List<Friendship>> GetPendingRequestsAsync(Guid userId)
    {
        return await _context.Friendships
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.FriendId == userId && f.Status == FriendshipStatus.Pending)
            .ToListAsync();
    }

    public async Task<List<Friendship>> GetOutgoingPendingRequestsAsync(Guid userId)
    {
        return await _context.Friendships
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.UserId == userId && f.Status == FriendshipStatus.Pending)
            .ToListAsync();
    }

    public async Task<Friendship> CancelOutgoingFriendRequestAsync(Guid friendshipId, Guid authorUserId)
    {
        var friendship = await _context.Friendships
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(f => f.Id == friendshipId);

        if (friendship == null)
            throw new InvalidOperationException("Friendship not found");

        if (friendship.Status != FriendshipStatus.Pending)
            throw new InvalidOperationException("Friendship is not pending");

        // SECURITY: Only the sender can cancel the outgoing request
        if (friendship.UserId != authorUserId)
            throw new UnauthorizedAccessException("You are not authorized to cancel this friend request");

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
        return friendship;
    }

    public async Task<List<User>> GetFriendsAsync(Guid userId)
    {
        var friendships = await _context.Friendships
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == FriendshipStatus.Accepted)
            .ToListAsync();

        var friends = friendships.Select(f => f.UserId == userId ? f.Friend : f.User).ToList();
        return friends;
    }

    public async Task<bool> AreFriendsAsync(Guid userId, Guid friendId)
    {
        return await _context.Friendships
            .AnyAsync(f =>
                ((f.UserId == userId && f.FriendId == friendId) ||
                 (f.UserId == friendId && f.FriendId == userId)) &&
                f.Status == FriendshipStatus.Accepted);
    }

    public async Task BlockUserAsync(Guid userId, Guid blockedUserId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                (f.UserId == userId && f.FriendId == blockedUserId) ||
                (f.UserId == blockedUserId && f.FriendId == userId));

        if (friendship == null)
        {
            friendship = new Friendship
            {
                UserId = userId,
                FriendId = blockedUserId,
                Status = FriendshipStatus.Blocked,
                CreatedAt = DateTime.UtcNow
            };
            _context.Friendships.Add(friendship);
        }
        else
        {
            friendship.Status = FriendshipStatus.Blocked;
        }

        await _context.SaveChangesAsync();
    }
}
