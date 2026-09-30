using System.Globalization;
using System.Numerics;

namespace CalculatriceMaui.Models;

/// <summary>Nombre rationnel exact : fraction irréductible, dénominateur strictement positif.</summary>
public readonly struct Rational
{
    private static readonly BigInteger Scale18 = BigInteger.Pow(10, 18);

    public static readonly Rational Zero = new(BigInteger.Zero, BigInteger.One);
    public static readonly Rational One = new(BigInteger.One, BigInteger.One);
    public static readonly Rational Hundred = new(new BigInteger(100), BigInteger.One);

    public BigInteger Num { get; }
    public BigInteger Den { get; }

    public Rational(BigInteger num, BigInteger den)
    {
        if (den.IsZero) throw new DivideByZeroException();
        if (den.Sign < 0) { num = -num; den = -den; }
        BigInteger g = BigInteger.GreatestCommonDivisor(num, den);
        if (!g.IsOne) { num /= g; den /= g; }
        Num = num;
        Den = den;
    }

    public bool IsZero => Num.IsZero;
    public bool IsInteger => Den.IsOne;
    public int Sign => Num.Sign;

    /// <summary>Inverse multiplicatif (le nombre ne doit pas être nul).</summary>
    public Rational Inverse() => new(Den, Num);

    public static Rational operator +(Rational a, Rational b) => new(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);
    public static Rational operator -(Rational a, Rational b) => new(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);
    public static Rational operator *(Rational a, Rational b) => new(a.Num * b.Num, a.Den * b.Den);
    public static Rational operator /(Rational a, Rational b) => new(a.Num * b.Den, a.Den * b.Num);
    public static Rational operator -(Rational a) => new(-a.Num, a.Den);

    /// <summary>Conversion exacte : 0,125 donne 1/8.</summary>
    public static Rational FromDecimal(decimal value)
    {
        string s = value.ToString("0.############################", CultureInfo.InvariantCulture);
        int dot = s.IndexOf('.');
        int scale = dot < 0 ? 0 : s.Length - dot - 1;
        BigInteger num = BigInteger.Parse(s.Replace(".", string.Empty), CultureInfo.InvariantCulture);
        return new Rational(num, BigInteger.Pow(10, scale));
    }

    /// <summary>Valeur approchée en double (pour les fonctions transcendantes).</summary>
    public double ToDouble() => (double)BigInteger.Divide(Num * Scale18, Den) / 1e18;
}
