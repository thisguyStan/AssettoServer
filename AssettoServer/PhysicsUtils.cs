﻿using System;

namespace AssettoServer
{
    public static class PhysicsUtils
    {
        private const float Gravity = 9.80665f;
        
        public static float CalculateBrakingDistance(float speed, float deceleration = Gravity)
        {
            return (float)Math.Pow(speed, 2) / (2 * deceleration);
        }
        
        public static float CalculateMaxCorneringSpeed(float radius, float friction = 1)
        {
            return (float)Math.Sqrt(Gravity * friction * radius);
        }
    }
}