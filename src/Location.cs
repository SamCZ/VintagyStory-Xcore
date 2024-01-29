using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VintageEx
{
    [Serializable]
    public class Location
    {
        public double X;
        public double Y;
        public double Z;
        
        public float Yaw;

        public Location(EntityPos pos)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
            Yaw = pos.Yaw;
        }
        
        public Location(Vec3d pos)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
            Yaw = 0;
        }

        public Location()
        { }

        public void Apply(EntityPos entityPos)
        {
            entityPos.SetPos(X, Y, Z);
            entityPos.SetYaw(Yaw);
        }

        public Vec3d AsVec3d()
        {
            return new Vec3d(X, Y, Z);
        }

        public EntityPos AsEntityPos()
        {
            return new EntityPos(X, Y, Z, Yaw);
        }
    }
}