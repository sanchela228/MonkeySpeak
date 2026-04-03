using System;
using System.Collections.Concurrent;
using System.Linq;
using Core.Database.Services;
using Core.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using ContextDatabase = Core.Database.Context;

namespace Core.Websockets;

public static class MessageDispatcherAuthExtensions
{
    public static void ConfigureAuthHandlers(
        this MessageDispatcher dispatcher,
        IServiceProvider serviceProvider,
        ConcurrentDictionary<Guid, Connection> connections,
        ConcurrentDictionary<string, Room> rooms,
        WebsocketMiddleware middleware)
    {
        dispatcher.On<Messages.AuthCall.RequestAuth>(async (msg, author) =>
        {
            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            
            try
            {
                var userId = Guid.Parse(msg.UserId);
                var user = await userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "User not found",
                        ErrorCode = "USER_NOT_FOUND"
                    });
                    return;
                }

                var nonce = Guid.NewGuid().ToString();
                author.AuthNonce = nonce;

                author.Send(new Messages.AuthCall.AuthChallenge
                {
                    Nonce = nonce,
                    Value = "Sign this nonce to authenticate"
                });

                Console.WriteLine($"Auth challenge sent to user {userId}: {nonce}");
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration
                {
                    Value = ex.Message,
                    ErrorCode = "AUTH_CHALLENGE_FAILED"
                });
            }
        });

        dispatcher.On<Messages.AuthCall.Authenticate>(async (msg, author) =>
        {
            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            
            try
            {
                if (string.IsNullOrEmpty(author.AuthNonce))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "No challenge nonce found. Request auth first.",
                        ErrorCode = "NO_CHALLENGE"
                    });
                    return;
                }

                var userId = Guid.Parse(msg.UserId);
                var user = await userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "User not found",
                        ErrorCode = "USER_NOT_FOUND"
                    });
                    return;
                }

                var signature = Convert.FromBase64String(msg.Signature);
                var nonceBytes = System.Text.Encoding.UTF8.GetBytes(author.AuthNonce);

                var isValid = userService.VerifySignature(user.PublicKeyEd25519, nonceBytes, signature);

                if (!isValid)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "Invalid signature",
                        ErrorCode = "INVALID_SIGNATURE"
                    });
                    return;
                }

                author.UserId = user.Id;
                author.IsAuthenticated = true;
                author.AuthNonce = null;

                author.Send(new Messages.AuthCall.Authenticated
                {
                    UserId = user.Id.ToString(),
                    Username = user.Username,
                    UserCode = user.UserCode,
                    Value = "Authentication successful"
                });

                await NotifyFriendsPresenceChange(author, true, connections, serviceProvider);

                Console.WriteLine($"User authenticated: {user.Username} ({user.Id})");
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration
                {
                    Value = ex.Message,
                    ErrorCode = "AUTHENTICATION_FAILED"
                });
            }
        });

        dispatcher.On<Messages.AuthCall.LoginRequest>(async (msg, author) =>
        {
            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();

            try
            {
                var user = await userService.VerifyPasswordAsync(msg.Username, msg.Password);
                if (user == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "Invalid username or password",
                        ErrorCode = "INVALID_CREDENTIALS"
                    });
                    return;
                }

                author.UserId = user.Id;
                author.IsAuthenticated = true;

                author.Send(new Messages.AuthCall.LoginSuccess
                {
                    UserId = user.Id.ToString(),
                    Username = user.Username,
                    UserCode = user.UserCode,
                    Value = "Login successful"
                });

                await NotifyFriendsPresenceChange(author, true, connections, serviceProvider);

                Console.WriteLine($"User logged in with password: {user.Username} ({user.Id})");
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration
                {
                    Value = ex.Message,
                    ErrorCode = "LOGIN_FAILED"
                });
            }
        });

        dispatcher.On<Messages.AuthCall.RegisterKey>(async (msg, author) =>
        {
            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            
            try
            {
                var publicKeyEd25519 = Convert.FromBase64String(msg.PublicKeyEd25519Base64);
                var publicKeyX25519 = Convert.FromBase64String(msg.PublicKeyX25519Base64);

                if (string.IsNullOrWhiteSpace(msg.Nonce) || string.IsNullOrWhiteSpace(msg.ProofSignature))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "Missing proof-of-possession fields",
                        ErrorCode = "MISSING_PROOF"
                    });
                    return;
                }

                byte[] proofSignature;
                try
                {
                    proofSignature = Convert.FromBase64String(msg.ProofSignature);
                }
                catch
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "Invalid proof signature encoding",
                        ErrorCode = "INVALID_PROOF_ENCODING"
                    });
                    return;
                }

                var nonceBytes = System.Text.Encoding.UTF8.GetBytes(msg.Nonce);
                var proofValid = userService.VerifySignature(publicKeyEd25519, nonceBytes, proofSignature);

                if (!proofValid)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration
                    {
                        Value = "Invalid proof signature",
                        ErrorCode = "INVALID_PROOF_SIGNATURE"
                    });
                    return;
                }

                var user = await userService.CreateUserAsync(msg.Username, publicKeyEd25519, publicKeyX25519);

                author.UserId = user.Id;
                author.IsAuthenticated = true;

                author.Send(new Messages.AuthCall.KeyRegistered
                {
                    UserId = user.Id.ToString(),
                    UserCode = user.UserCode,
                    Fingerprint = user.KeyFingerprint,
                    Value = "Registration successful"
                });

                await NotifyFriendsPresenceChange(author, true, connections, serviceProvider);

                Console.WriteLine($"User registered: {user.Username} ({user.Id})");
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration
                {
                    Value = ex.Message,
                    ErrorCode = "REGISTRATION_FAILED"
                });
            }
        });

        dispatcher.On<Messages.AuthCall.AddFriend>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var handle = (msg.FriendUsername ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(handle))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Empty friend handle", ErrorCode = "INVALID_FRIEND_HANDLE" });
                    return;
                }

                Core.Database.Models.User? friend;
                var hashIdx = handle.LastIndexOf('#');
                if (hashIdx > 0 && hashIdx < handle.Length - 1)
                {
                    var username = handle[..hashIdx].Trim();
                    var code = handle[(hashIdx + 1)..].Trim().ToLowerInvariant();
                    friend = await userService.GetUserByHandleAsync(username, code);
                }
                else
                {
                    var users = await userService.GetUsersByUsernameAsync(handle);
                    if (users.Count == 0)
                    {
                        author.Send(new Messages.AuthCall.ErrorRegistration { Value = "User not found", ErrorCode = "USER_NOT_FOUND" });
                        return;
                    }

                    if (users.Count > 1)
                    {
                        author.Send(new Messages.AuthCall.ErrorRegistration
                        {
                            Value = "Username is ambiguous. Use username#code.",
                            ErrorCode = "USERNAME_AMBIGUOUS"
                        });
                        return;
                    }

                    friend = users[0];
                }

                if (friend == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "User not found", ErrorCode = "USER_NOT_FOUND" });
                    return;
                }

                var friendship = await friendshipService.SendFriendRequestAsync(author.UserId.Value, friend.Id);

                author.Send(new Messages.AuthCall.FriendRequestSent
                {
                    FriendshipId = friendship.Id.ToString(),
                    FriendId = friend.Id.ToString(),
                    FriendUsername = friend.Username,
                    FriendUserCode = friend.UserCode,
                    Value = "Friend request sent"
                });

                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friend.Id && c.IsAuthenticated);
                if (friendConnection != null)
                {
                    var senderUser = await userService.GetUserByIdAsync(author.UserId.Value);
                    friendConnection.Send(new Messages.AuthCall.FriendRequestReceived
                    {
                        FriendshipId = friendship.Id.ToString(),
                        FromUserId = author.UserId.Value.ToString(),
                        FromUsername = senderUser?.Username ?? "Unknown",
                        FromUserCode = senderUser?.UserCode ?? string.Empty,
                        Value = "New friend request"
                    });
                }
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "ADD_FRIEND_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.AcceptFriend>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var friendshipId = Guid.Parse(msg.FriendshipId);
                var friendship = await friendshipService.AcceptFriendRequestAsync(friendshipId, author.UserId.Value);

                var friendUser = await userService.GetUserByIdAsync(friendship.UserId);

                author.Send(new Messages.AuthCall.FriendAccepted
                {
                    FriendId = friendship.UserId.ToString(),
                    FriendUsername = friendUser?.Username ?? "Unknown",
                    Value = "Friend request accepted"
                });

                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friendship.UserId && c.IsAuthenticated);
                if (friendConnection != null)
                {
                    var acceptorUser = await userService.GetUserByIdAsync(author.UserId.Value);
                    friendConnection.Send(new Messages.AuthCall.FriendAccepted
                    {
                        FriendId = author.UserId.Value.ToString(),
                        FriendUsername = acceptorUser?.Username ?? "Unknown",
                        Value = "Friend request accepted"
                    });
                }
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "ACCEPT_FRIEND_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.RejectFriend>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var friendshipId = Guid.Parse(msg.FriendshipId);

                var friendship = await friendshipService.GetFriendshipByIdAsync(friendshipId);
                await friendshipService.RemoveFriendshipByIdAsync(friendshipId, author.UserId.Value);

                author.Send(new Messages.AuthCall.FriendRequestRejected
                {
                    FriendshipId = friendshipId.ToString(),
                    Value = "Friend request rejected"
                });

                if (friendship != null)
                {
                    var senderUserId = friendship.UserId;
                    var senderConnection = connections.Values.FirstOrDefault(c => c.UserId == senderUserId && c.IsAuthenticated);
                    senderConnection?.Send(new Messages.AuthCall.FriendRequestRejected
                    {
                        FriendshipId = friendshipId.ToString(),
                        Value = "Friend request rejected"
                    });
                }

                Console.WriteLine($"User {author.UserId} rejected friend request: {friendshipId}");
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "REJECT_FRIEND_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.RemoveFriend>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var friendId = Guid.Parse(msg.FriendId);
                await friendshipService.RemoveFriendAsync(author.UserId.Value, friendId);

                author.Send(new Messages.AuthCall.FriendRemoved
                {
                    FriendId = friendId.ToString(),
                    Value = "Friend removed"
                });

                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friendId && c.IsAuthenticated);
                if (friendConnection != null)
                {
                    friendConnection.Send(new Messages.AuthCall.FriendRemoved
                    {
                        FriendId = author.UserId.Value.ToString(),
                        Value = "Friend removed"
                    });
                }
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "REMOVE_FRIEND_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.GetFriendList>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var friends = await friendshipService.GetFriendsAsync(author.UserId.Value);

                var friendInfos = friends.Select(f => new Messages.AuthCall.FriendInfo
                {
                    UserId = f.Id.ToString(),
                    Username = f.Username,
                    UserCode = f.UserCode,
                    IsOnline = connections.Values.Any(c => c.UserId == f.Id && c.IsAuthenticated),
                    LastSeenAt = f.LastSeenAt.ToString("o")
                }).ToList();
                
                author.Send(new Messages.AuthCall.FriendListResponse
                {
                    Friends = friendInfos,
                    Value = "Friend list retrieved"
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "GET_FRIENDS_FAILED" });
            }
        });
        
        dispatcher.On<Messages.AuthCall.GetPendingFriendList>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var friends = await friendshipService.GetPendingRequestsAsync(author.UserId.Value);

                var friendInfos = friends.Select(f => new Messages.AuthCall.FriendRequestReceived
                {
                   FriendshipId = f.Id.ToString(),
                   FromUserId = f.User.Id.ToString(),
                   FromUsername = f.User.Username.ToString(),
                   FromUserCode = f.User.UserCode,
                   Value = "Friend pending request received"
                }).ToList();
                
                author.Send(new Messages.AuthCall.PendingFriendListResponse
                {
                    Friends = friendInfos,
                    Value = "Friend pending list retrieved"
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "GET_FRIENDS_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.GetOutgoingPendingFriendList>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();

            try
            {
                var outgoing = await friendshipService.GetOutgoingPendingRequestsAsync(author.UserId.Value);

                var items = outgoing.Select(f => new Messages.AuthCall.OutgoingFriendRequestInfo
                {
                    FriendshipId = f.Id.ToString(),
                    ToUserId = f.Friend.Id.ToString(),
                    ToUsername = f.Friend.Username,
                    ToUserCode = f.Friend.UserCode
                }).ToList();

                author.Send(new Messages.AuthCall.OutgoingPendingFriendListResponse
                {
                    Friends = items,
                    Value = "Outgoing pending friend list retrieved"
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "GET_OUTGOING_PENDING_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.CancelFriendRequest>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();

            try
            {
                var friendshipId = Guid.Parse(msg.FriendshipId);
                var friendship = await friendshipService.CancelOutgoingFriendRequestAsync(friendshipId, author.UserId.Value);

                // notify sender
                author.Send(new Messages.AuthCall.FriendRequestCancelled
                {
                    FriendshipId = friendshipId.ToString(),
                    Value = "Friend request cancelled"
                });

                // notify recipient (so their incoming pending list closes)
                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friendship.FriendId && c.IsAuthenticated);
                friendConnection?.Send(new Messages.AuthCall.FriendRequestCancelled
                {
                    FriendshipId = friendshipId.ToString(),
                    Value = "Friend request cancelled"
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "CANCEL_FRIEND_REQUEST_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.GetPublicKeys>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                var targetUserId = Guid.Parse(msg.UserId);

                if (!await friendshipService.AreFriendsAsync(author.UserId.Value, targetUserId))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not friends with this user", ErrorCode = "NOT_FRIENDS" });
                    return;
                }

                var (pubKeyEd25519, pubKeyX25519) = await userService.GetPublicKeysAsync(targetUserId);
                var user = await userService.GetUserByIdAsync(targetUserId);

                author.Send(new Messages.AuthCall.PublicKeysResponse
                {
                    UserId = targetUserId.ToString(),
                    PublicKeyEd25519Base64 = Convert.ToBase64String(pubKeyEd25519),
                    PublicKeyX25519Base64 = Convert.ToBase64String(pubKeyX25519),
                    Fingerprint = user?.KeyFingerprint ?? "",
                    Value = "Public keys retrieved"
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "GET_KEYS_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.InitiateCall>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            
            try
            {
                author.PublicIp = msg.IpEndPoint;

                var friendId = Guid.Parse(msg.FriendId);
                
                if (!await friendshipService.AreFriendsAsync(author.UserId.Value, friendId))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not friends with this user", ErrorCode = "NOT_FRIENDS" });
                    return;
                }

                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friendId && c.IsAuthenticated);
                if (friendConnection == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Friend is offline", ErrorCode = "FRIEND_OFFLINE" });
                    return;
                }

                var room = new Room(author);
                rooms.TryAdd(room.Code, room);
                author.Status = Connection.StatusConnection.Connected;

                var callerUser = await userService.GetUserByIdAsync(author.UserId.Value);

                friendConnection.Send(new Messages.AuthCall.IncomingCall
                {
                    RoomCode = room.Code,
                    FromUserId = author.UserId.Value.ToString(),
                    FromUsername = callerUser?.Username ?? "Unknown",
                    Value = "Incoming call"
                });

                Console.WriteLine($"Call initiated from {author.UserId} to {friendId}, room code: {room.Code}");
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "INITIATE_CALL_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.InviteToCall>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();

            try
            {
                if (string.IsNullOrWhiteSpace(msg.FriendId) || string.IsNullOrWhiteSpace(msg.RoomCode))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Invalid parameters", ErrorCode = "INVALID_PARAMS" });
                    return;
                }

                var friendId = Guid.Parse(msg.FriendId);

                if (!await friendshipService.AreFriendsAsync(author.UserId.Value, friendId))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not friends with this user", ErrorCode = "NOT_FRIENDS" });
                    return;
                }

                if (!rooms.TryGetValue(msg.RoomCode, out var room) || !room.Connections.ContainsKey(author.Id))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Room not found", ErrorCode = "ROOM_NOT_FOUND" });
                    return;
                }

                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friendId && c.IsAuthenticated);
                if (friendConnection == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Friend is offline", ErrorCode = "FRIEND_OFFLINE" });
                    return;
                }

                var callerUser = await userService.GetUserByIdAsync(author.UserId.Value);

                friendConnection.Send(new Messages.AuthCall.IncomingCall
                {
                    RoomCode = msg.RoomCode,
                    FromUserId = author.UserId.Value.ToString(),
                    FromUsername = callerUser?.Username ?? "Unknown",
                    Value = "Incoming call"
                });

                author.Send(new Messages.AuthCall.CallInviteSent
                {
                    FriendId = friendId.ToString(),
                    RoomCode = msg.RoomCode,
                    Value = "Invite sent"
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "INVITE_CALL_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.CallInviteResponse>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();

            try
            {
                if (string.IsNullOrWhiteSpace(msg.ToUserId) || string.IsNullOrWhiteSpace(msg.RoomCode))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Invalid parameters", ErrorCode = "INVALID_PARAMS" });
                    return;
                }

                var toUserId = Guid.Parse(msg.ToUserId);

                if (!await friendshipService.AreFriendsAsync(author.UserId.Value, toUserId))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not friends with this user", ErrorCode = "NOT_FRIENDS" });
                    return;
                }

                // If callee tries to accept/reject after caller cancelled (or room already gone), do not proxy "accept".
                if (msg.Accepted)
                {
                    if (!rooms.TryGetValue(msg.RoomCode, out var room)
                        || room.State != Room.RoomState.Waiting
                        || !room.Connections.Values.Any(c => c.UserId == toUserId))
                    {
                        author.Send(new Messages.AuthCall.ErrorRegistration
                        {
                            Value = "Call has expired",
                            ErrorCode = "CALL_EXPIRED"
                        });
                        return;
                    }
                }

                var target = connections.Values.FirstOrDefault(c => c.UserId == toUserId && c.IsAuthenticated);
                if (target == null)
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Target offline", ErrorCode = "TARGET_OFFLINE" });
                    return;
                }

                target.Send(new Messages.AuthCall.CallInviteResponse
                {
                    RoomCode = msg.RoomCode,
                    ToUserId = msg.ToUserId,
                    FromUserId = author.UserId.Value.ToString(),
                    FromUsername = msg.FromUsername,
                    Accepted = msg.Accepted,
                    Reason = msg.Reason,
                    Value = msg.Value
                });
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "CALL_INVITE_RESPONSE_FAILED" });
            }
        });

        dispatcher.On<Messages.AuthCall.CancelCallInvite>(async (msg, author) =>
        {
            if (!author.IsAuthenticated || author.UserId == null)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not authenticated", ErrorCode = "NOT_AUTHENTICATED" });
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();

            try
            {
                if (string.IsNullOrWhiteSpace(msg.FriendId) || string.IsNullOrWhiteSpace(msg.RoomCode))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Invalid parameters", ErrorCode = "INVALID_PARAMS" });
                    return;
                }

                var friendId = Guid.Parse(msg.FriendId);

                if (!await friendshipService.AreFriendsAsync(author.UserId.Value, friendId))
                {
                    author.Send(new Messages.AuthCall.ErrorRegistration { Value = "Not friends with this user", ErrorCode = "NOT_FRIENDS" });
                    return;
                }

                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friendId && c.IsAuthenticated);
                if (friendConnection == null)
                    return;

                var callerUser = await userService.GetUserByIdAsync(author.UserId.Value);

                friendConnection.Send(new Messages.AuthCall.CallInviteCancelled
                {
                    RoomCode = msg.RoomCode,
                    FromUserId = author.UserId.Value.ToString(),
                    FromUsername = callerUser?.Username ?? "Unknown",
                    Value = "Invite cancelled"
                });

                // Ensure the callee cannot "accept" a cancelled ringing call.
                if (rooms.TryGetValue(msg.RoomCode, out var room)
                    && room.State == Room.RoomState.Waiting
                    && room.Connections.Count <= 1
                    && room.Connections.ContainsKey(author.Id))
                {
                    rooms.TryRemove(msg.RoomCode, out _);
                    author.Status = Connection.StatusConnection.Idle;
                }
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration { Value = ex.Message, ErrorCode = "CANCEL_CALL_INVITE_FAILED" });
            }
        });
        dispatcher.On<Messages.AuthCall.ChangeUsername>(async (msg, author) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var userService = scope.ServiceProvider.GetRequiredService<UserService>();
                var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();

                var user = await userService.UpdateUsernameAsync(author.UserId!.Value, msg.NewUsername);

                author.Send(new Messages.AuthCall.UsernameChanged
                {
                    NewUsername = user.Username,
                    Value = "Username changed successfully"
                });

                var friends = await friendshipService.GetFriendsAsync(author.UserId.Value);
                foreach (var friend in friends)
                {
                    var friendConn = connections.Values.FirstOrDefault(c => c.UserId == friend.Id && c.IsAuthenticated);
                    friendConn?.Send(new Messages.AuthCall.FriendUsernameChanged
                    {
                        FriendId = author.UserId.Value.ToString(),
                        NewUsername = user.Username,
                        Value = "Friend changed username"
                    });
                }
            }
            catch (Exception ex)
            {
                author.Send(new Messages.AuthCall.ErrorRegistration
                {
                    Value = ex.Message,
                    ErrorCode = "CHANGE_USERNAME_FAILED"
                });
            }
        });
    }

    public static async Task NotifyFriendsPresenceChange(
        Connection connection,
        bool isOnline,
        ConcurrentDictionary<Guid, Connection> connections,
        IServiceProvider serviceProvider)
    {
        if (!connection.IsAuthenticated || connection.UserId == null)
            return;

        try
        {
            using var scope = serviceProvider.CreateScope();
            var friendshipService = scope.ServiceProvider.GetRequiredService<FriendshipService>();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            
            var friends = await friendshipService.GetFriendsAsync(connection.UserId.Value);
            var user = await userService.GetUserByIdAsync(connection.UserId.Value);

            foreach (var friend in friends)
            {
                var friendConnection = connections.Values.FirstOrDefault(c => c.UserId == friend.Id && c.IsAuthenticated);
                if (friendConnection != null)
                {
                    if (isOnline)
                    {
                        friendConnection.Send(new Messages.AuthCall.FriendOnline
                        {
                            FriendId = connection.UserId.Value.ToString(),
                            FriendUsername = user?.Username ?? "Unknown",
                            Value = "Friend is now online"
                        });
                    }
                    else
                    {
                        friendConnection.Send(new Messages.AuthCall.FriendOffline
                        {
                            FriendId = connection.UserId.Value.ToString(),
                            FriendUsername = user?.Username ?? "Unknown",
                            Value = "Friend is now offline"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error notifying friends of presence change: {ex.Message}");
        }
    }
}
