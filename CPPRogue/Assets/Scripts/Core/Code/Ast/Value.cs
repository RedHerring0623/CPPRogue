namespace CPPRogue.Core.Code.Ast
{
    /// <summary>值的种类。为以后的扩展（目标引用、字符串）预留。</summary>
    public enum ValueKind
    {
        Number,
        Bool,
    }

    /// <summary>
    /// 脚本值的统一表示：数字和布尔共用一个 double 载体。
    /// attack(n)、if (hp &lt; 30%) 里的 n、hp、30% 在运行期都是 Value。
    /// </summary>
    public readonly struct Value
    {
        public ValueKind Kind { get; }
        public double Number { get; }

        private Value(ValueKind kind, double number)
        {
            Kind = kind;
            Number = number;
        }

        public static Value Zero { get; } = new Value(ValueKind.Number, 0d);

        public static Value Of(double number) => new Value(ValueKind.Number, number);

        public static Value Of(bool b) => new Value(ValueKind.Bool, b ? 1d : 0d);

        public double AsNumber() => Number;

        public bool AsBool() => Number != 0d;

        public override string ToString()
        {
            return Kind == ValueKind.Bool
                ? (Number != 0d ? "true" : "false")
                : Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
