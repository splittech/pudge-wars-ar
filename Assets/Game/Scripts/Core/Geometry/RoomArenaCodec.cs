using System;
using System.IO;
using Unity.Mathematics;

namespace Game.Core.Geometry
{
    public static class RoomArenaCodec
    {
        public const int MaximumBytes = 9000;

        public static byte[] Encode(RoomArena room)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)1);
                writer.Write((byte)room.PolygonCount);
                for (int i = 0; i < room.PolygonCount; i++)
                {
                    var polygon = room.GetPolygon(i);
                    writer.Write((byte)polygon.Count);
                    foreach (float2 point in polygon)
                    {
                        writer.Write(point.x);
                        writer.Write(point.y);
                    }
                }

                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] payload, out RoomArena room, out string reason)
        {
            room = null;
            reason = "Карта комнаты повреждена или превышает ограничения.";
            if (payload == null || payload.Length < 2 || payload.Length > MaximumBytes)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadByte() != 1)
                    {
                        return false;
                    }

                    int count = reader.ReadByte();
                    if (count < 1 || count > RoomArena.MaximumObstacles + 1)
                    {
                        return false;
                    }

                    var polygons = new float2[count][];
                    for (int i = 0; i < count; i++)
                    {
                        int vertices = reader.ReadByte();
                        if (vertices < 3 || vertices > RoomArena.MaximumVertices || stream.Length - stream.Position < vertices * 8)
                        {
                            return false;
                        }

                        polygons[i] = new float2[vertices];
                        for (int j = 0; j < vertices; j++)
                        {
                            polygons[i][j] = new float2(reader.ReadSingle(), reader.ReadSingle());
                        }
                    }

                    if (stream.Position != stream.Length)
                    {
                        return false;
                    }

                    var obstacles = new float2[count - 1][];
                    Array.Copy(polygons, 1, obstacles, 0, obstacles.Length);
                    room = new RoomArena(polygons[0], obstacles);
                    reason = string.Empty;
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                reason = exception.Message;
                return false;
            }
        }
    }
}
