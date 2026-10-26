using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Shared.Results;
using Basera.Shared.Time;

namespace Basera.Modules.Properties.Application;

/// <summary>A room as the owner describes it. <paramref name="DormBeds"/> counts only for a dorm.</summary>
public sealed record RoomDetails(int FloorNumber, string? Number, RoomType Type, int? DormBeds, RoomAttributes Attributes, string? Notes);

/// <summary>Floors and rooms of one property (PRODUCT_SPEC §4.2). Beds follow from the room type.</summary>
public sealed class RoomService(PropertyStore store, IClock clock)
{
    public Task<Result<Guid>> AddFloorAsync(Guid propertyId, int number, string? label, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property => property.AddFloor(number, label).Map(floor => floor.Id), cancellationToken);

    public Task<Result> UpdateFloorAsync(Guid propertyId, Guid floorId, int number, string? label, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property => property.UpdateFloor(floorId, number, label), cancellationToken);

    public Task<Result> RemoveFloorAsync(Guid propertyId, Guid floorId, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property => property.RemoveFloor(floorId), cancellationToken);

    public Task<Result<Guid>> AddRoomAsync(Guid propertyId, RoomDetails room, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(
            propertyId,
            property => property.AddRoom(room.FloorNumber, room.Number, room.Type, room.DormBeds, room.Attributes, room.Notes).Map(r => r.Id),
            cancellationToken);

    /// <summary>Changes a room, including moving it to another floor; its bed codes follow.</summary>
    public Task<Result> UpdateRoomAsync(Guid propertyId, Guid roomId, RoomDetails room, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property =>
        {
            if (property.FindRoom(roomId) is { } existing && existing.FloorNumber != room.FloorNumber)
            {
                var moved = property.MoveRoom(roomId, room.FloorNumber);
                if (moved.IsFailure)
                {
                    return moved;
                }
            }

            return property.UpdateRoom(roomId, room.Number, room.Type, room.DormBeds, room.Attributes, room.Notes, clock.Today);
        }, cancellationToken);

    public Task<Result> RemoveRoomAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property => property.RemoveRoom(roomId, clock.Today), cancellationToken);
}
