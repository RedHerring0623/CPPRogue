namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 敌人模拟用的二维向量。Core.Physics 直接用 UnityEngine.Vector2（依赖引擎 DLL，独立测试工程编译不了），
    /// 敌人模拟要求在没有 Unity 的机器上跑 dotnet test，所以自带一份极简数学。
    /// </summary>
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public float SqrMagnitude => X * X + Y * Y;

        public float Magnitude => (float)System.Math.Sqrt(X * X + Y * Y);

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Magnitude;

        public static Vec2 FromAngle(float radians) =>
            new Vec2((float)System.Math.Cos(radians), (float)System.Math.Sin(radians));

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 v) => new Vec2(-v.X, -v.Y);
        public static Vec2 operator *(Vec2 v, float s) => new Vec2(v.X * s, v.Y * s);
        public static Vec2 operator *(float s, Vec2 v) => v * s;
        public static Vec2 operator /(Vec2 v, float s) => new Vec2(v.X / s, v.Y / s);

        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }
}
