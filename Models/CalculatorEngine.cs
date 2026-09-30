using System.Globalization;
using System.Numerics;
using System.Text;

namespace CalculatriceMaui.Models;

public enum UnaryOperation
{
    SquareRoot, Square, Inverse,
    Sin, Cos, Tan, Asin, Acos, Atan,
    Ln, Log, Factorial
}

public enum ResultFormat { Decimal, Fraction, Scientific }

public enum MathConstant { Pi, E }

/// <summary>
/// Moteur de calcul indépendant de l'interface (aucune dépendance MAUI).
/// Les nombres sont des rationnels exacts (<see cref="Rational"/>) : 0,1 + 0,2 = 0,3 et 1 ÷ 3 × 3 = 1.
/// Les fonctions transcendantes passent par le type double, puis le résultat est arrondi à 12 décimales.
/// </summary>
public sealed class CalculatorEngine
{
    private const int MaxDigits = 15;
    private const int MaxDecimals = 10;
    private const decimal MaxValue = 7.9e28m;
    private const int MaxExactDenominator = 1_000_000; // au-delà, la valeur est affichée en décimal
    private const int MaxIntegerPower = 200;
    private const int MaxFactorial = 27;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly BigInteger MaxBig = BigInteger.Pow(10, 27) * 79; // 7,9 × 10^28

    private string _entry = "0";          // saisie en cours ("12.5", "3/4"), format invariant
    private Rational _value = Rational.Zero; // valeur affichée lorsqu'on ne saisit plus
    private bool _typing = true;          // true : l'utilisateur est en train de saisir _entry
    private bool _awaitingOperand;        // true : un opérateur vient d'être pressé
    private Rational? _left;              // opérande de gauche en attente
    private char? _op;                    // opérateur en attente
    private char? _repeatOp;              // pour la répétition de "="
    private Rational _repeatOperand = Rational.Zero;
    private string _expression = string.Empty;
    private string? _error;
    private Rational _memory = Rational.Zero;
    private bool _autoFraction;           // affichage fractionnaire activé automatiquement par la saisie d'une fraction

    public bool HasError => _error is not null;
    public bool HasMemory => !_memory.IsZero;
    public bool UseDegrees { get; private set; } = true;
    public ResultFormat OutputFormat { get; private set; } = ResultFormat.Decimal;
    public string Expression => _expression;

    public string Display
    {
        get
        {
            if (_error is not null) return _error;
            if (_typing) return Pretty(_entry);
            return OutputFormat switch
            {
                ResultFormat.Fraction => FractionText(_value),
                ResultFormat.Scientific => ScientificText(ToPlain(_value)),
                _ => Pretty(ToPlain(_value))
            };
        }
    }

    private Rational Current => _typing ? ParseEntry(_entry) : _value;

    // ───────────────────────── Saisie ─────────────────────────

    public void InputDigit(int digit)
    {
        BeginEntry();
        if (_entry.EndsWith('/') && digit == 0) return; // un dénominateur nul ne peut pas être saisi
        if (_entry == "0") _entry = digit.ToString(Inv);
        else if (_entry == "-0") _entry = "-" + digit.ToString(Inv);
        else if (_entry.Count(char.IsDigit) < MaxDigits) _entry += digit.ToString(Inv);
    }

    public void InputDecimal()
    {
        BeginEntry();
        if (_entry.Contains('/')) return; // pas de virgule dans une fraction
        if (!_entry.Contains('.')) _entry += ".";
    }

    /// <summary>Barre de fraction : « 3 » puis « / » puis « 4 » saisit 3/4. Le résultat s'affiche alors d'abord en fraction.</summary>
    public void InputFractionBar()
    {
        BeginEntry();
        if (_entry.Contains('/') || _entry.Contains('.')) return; // numérateur entier requis
        _entry += "/";
        if (OutputFormat == ResultFormat.Decimal)
        {
            OutputFormat = ResultFormat.Fraction;
            _autoFraction = true;
        }
    }

    public void Backspace()
    {
        if (_error is not null) { ResetAll(); return; }
        if (!_typing) return; // on n'efface pas un résultat
        _entry = _entry[..^1];
        if (_entry is "" or "-" or "-0") _entry = "0";
    }

    public void Clear() => ResetAll(); // la mémoire n'est pas effacée par C (voir MemoryClear)

    public void ToggleSign()
    {
        if (_error is not null) return;
        if (!_typing && !_awaitingOperand)
        {
            SetValue(-Current); // résultat affiché : on l'inverse
            return;
        }
        BeginEntry();
        _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
    }

    public void Percent()
    {
        if (_error is not null) return;
        Rational v = Current;
        Rational r = (_op is '+' or '−') && _left is Rational l
            ? l * v / Rational.Hundred   // 200 + 10 % => 200 + 20
            : v / Rational.Hundred;
        _expression = $"{Prefix()}{Fmt(v)} %";
        if (_op is null) _repeatOp = null;
        if (!CheckRange(r)) return;
        SetValue(r);
    }

    // ───────────────────────── Opérations ─────────────────────────

    /// <summary>Opérateurs : + − × ÷ ^ ; C = combinaisons C(n,p) ; A = arrangements A(n,p).</summary>
    public void SetOperator(char op)
    {
        if (_error is not null) return;

        if (_awaitingOperand)
        {
            _op = op; // changement d'opérateur sans nouvelle saisie
            _expression = $"{Fmt(_left!.Value)} {op}";
            return;
        }

        Rational v = Current;
        if (_op is char pending && _left is Rational l)
        {
            _expression = $"{Fmt(l)} {pending} {Fmt(v)}";
            if (!TryCompute(l, pending, v, out Rational r)) return; // enchaînement : 12 + 7 × => 19 ×
            _left = r;
            _value = r;
        }
        else
        {
            _left = v;
            _value = v;
        }

        _op = op;
        _repeatOp = null;
        _awaitingOperand = true;
        _typing = false;
        _expression = $"{Fmt(_left.Value)} {op}";
    }

    public void Evaluate()
    {
        if (_error is not null) return;

        Rational right = Current, left;
        char op;
        if (_op is char pending && _left is Rational l) { left = l; op = pending; }
        else if (_repeatOp is char rep) { left = right; op = rep; right = _repeatOperand; }
        else return;

        _expression = $"{Fmt(left)} {op} {Fmt(right)} =";
        if (!TryCompute(left, op, right, out Rational result)) return;

        SetValue(result);
        _left = null;
        _op = null;
        _repeatOp = op;
        _repeatOperand = right;
    }

    // ───────────────────────── Fonctions avancées ─────────────────────────

    /// <summary>Fonctions à un opérande : racine, carré, inverse, trigonométrie, logarithmes, factorielle.</summary>
    public void ApplyUnary(UnaryOperation kind)
    {
        if (_error is not null) return;
        Rational v = Current;
        string arg = Fmt(v);
        string unit = UseDegrees ? "°" : string.Empty;
        _expression = Prefix() + (kind switch
        {
            UnaryOperation.SquareRoot => $"√({arg})",
            UnaryOperation.Square => $"({arg})²",
            UnaryOperation.Inverse => $"1/({arg})",
            UnaryOperation.Sin => $"sin({arg}{unit})",
            UnaryOperation.Cos => $"cos({arg}{unit})",
            UnaryOperation.Tan => $"tan({arg}{unit})",
            UnaryOperation.Asin => $"asin({arg})",
            UnaryOperation.Acos => $"acos({arg})",
            UnaryOperation.Atan => $"atan({arg})",
            UnaryOperation.Ln => $"ln({arg})",
            UnaryOperation.Log => $"log({arg})",
            UnaryOperation.Factorial => $"{arg}!",
            _ => arg
        });
        if (_op is null) _repeatOp = null;

        if (!TryUnary(kind, v, out Rational r) || !CheckRange(r)) return;
        SetValue(r);
    }

    /// <summary>Bascule degrés / radians (utilisé par sin, cos, tan et leurs réciproques).</summary>
    public void ToggleAngleUnit() => UseDegrees = !UseDegrees;

    /// <summary>Choisit la forme d'affichage : décimale, rationnelle (fraction) ou scientifique.</summary>
    public void SetOutputFormat(ResultFormat format)
    {
        OutputFormat = format;
        _autoFraction = false; // choix explicite de l'utilisateur : on ne le modifie plus automatiquement
        if (_error is null) Commit();
    }

    public void InsertConstant(MathConstant constant) =>
        InsertValue(Rational.FromDecimal(constant == MathConstant.Pi ? 3.14159265358979m : 2.71828182845905m));

    // ───────────────────────── Mémoire ─────────────────────────

    public void MemoryClear() => _memory = Rational.Zero;

    public void MemoryRecall()
    {
        if (_error is null) InsertValue(_memory);
    }

    public void MemoryStore()
    {
        if (_error is not null) return;
        _memory = Current;
        Commit();
    }

    public void MemoryAdd() => MemoryShift(add: true);

    public void MemorySubtract() => MemoryShift(add: false);

    private void MemoryShift(bool add)
    {
        if (_error is not null) return;
        Rational next = add ? _memory + Current : _memory - Current;
        if (!CheckRange(next)) return;
        _memory = next;
        Commit();
    }

    // ───────────────────────── Calculs ─────────────────────────

    private bool TryCompute(Rational a, char op, Rational b, out Rational result)
    {
        result = Rational.Zero;
        switch (op)
        {
            case '+': result = a + b; break;
            case '−': result = a - b; break;
            case '×': result = a * b; break;
            case '÷':
                if (b.IsZero) return Fail("Division par zéro impossible");
                result = a / b;
                break;
            case '^': return TryPower(a, b, out result);
            case 'C': return TryCounting(a, b, ordered: false, out result);
            case 'A': return TryCounting(a, b, ordered: true, out result);
            default: return false;
        }
        return CheckRange(result);
    }

    private bool TryPower(Rational a, Rational b, out Rational result)
    {
        result = Rational.Zero;

        // Exposant entier raisonnable : calcul exact sur les fractions
        if (b.IsInteger && BigInteger.Abs(b.Num) <= MaxIntegerPower)
        {
            int n = (int)b.Num;
            if (a.IsZero && n < 0) return Fail("Division par zéro impossible");
            Rational baseValue = n >= 0 ? a : a.Inverse();
            int e = Math.Abs(n);
            result = new Rational(BigInteger.Pow(baseValue.Num, e), BigInteger.Pow(baseValue.Den, e));
            return CheckRange(result);
        }

        double r = Math.Pow(a.ToDouble(), b.ToDouble());
        if (double.IsNaN(r)) return Fail("Entrée invalide");
        if (double.IsInfinity(r))
            return Fail(a.IsZero && b.Sign < 0 ? "Division par zéro impossible" : "Résultat trop grand");
        return FromDouble(r, out result);
    }

    /// <summary>C(n,p) = n! / (p! (n-p)!) et A(n,p) = n! / (n-p)!, calculés exactement en entiers.</summary>
    private bool TryCounting(Rational a, Rational b, bool ordered, out Rational result)
    {
        result = Rational.Zero;
        if (!a.IsInteger || !b.IsInteger || a.Sign < 0 || b.Sign < 0 || b.Num > a.Num)
            return Fail("Entrée invalide"); // n et p entiers, 0 ≤ p ≤ n

        BigInteger n = a.Num, p = b.Num;
        BigInteger k = ordered ? p : BigInteger.Min(p, n - p);
        BigInteger acc = BigInteger.One;
        for (BigInteger i = 1; i <= k; i++)
        {
            acc = ordered
                ? acc * (n - i + 1)               // A(n,p) = n (n-1) ... (n-p+1)
                : acc * (n - k + i) / i;          // C(n,k) : chaque étape reste un entier exact
            if (acc >= MaxBig) return Fail("Résultat trop grand");
        }
        result = new Rational(acc, BigInteger.One);
        return true;
    }

    private bool TryUnary(UnaryOperation kind, Rational v, out Rational result)
    {
        result = Rational.Zero;
        double x = v.ToDouble();

        switch (kind)
        {
            case UnaryOperation.SquareRoot:
            {
                if (v.Sign < 0) return Fail("Entrée invalide");
                // racine exacte quand numérateur et dénominateur sont des carrés parfaits (9/4 donne 3/2)
                BigInteger sn = ISqrt(v.Num), sd = ISqrt(v.Den);
                if (sn * sn == v.Num && sd * sd == v.Den)
                {
                    result = new Rational(sn, sd);
                    return true;
                }
                return FromDouble(Math.Sqrt(x), out result);
            }

            case UnaryOperation.Square:
                result = v * v;
                return true;

            case UnaryOperation.Inverse:
                if (v.IsZero) return Fail("Division par zéro impossible");
                result = v.Inverse();
                return true;

            case UnaryOperation.Sin:
                return FromDouble(Math.Sin(ToRadians(x)), out result);

            case UnaryOperation.Cos:
                return FromDouble(Math.Cos(ToRadians(x)), out result);

            case UnaryOperation.Tan:
            {
                double rad = ToRadians(x);
                if (Math.Abs(Math.Cos(rad)) < 1e-12) return Fail("Tangente indéfinie");
                return FromDouble(Math.Tan(rad), out result);
            }

            case UnaryOperation.Asin:
                if (v.Sign != 0 && BigInteger.Abs(v.Num) > v.Den) return Fail("Entrée invalide");
                return FromDouble(FromRadians(Math.Asin(x)), out result);

            case UnaryOperation.Acos:
                if (v.Sign != 0 && BigInteger.Abs(v.Num) > v.Den) return Fail("Entrée invalide");
                return FromDouble(FromRadians(Math.Acos(x)), out result);

            case UnaryOperation.Atan:
                return FromDouble(FromRadians(Math.Atan(x)), out result);

            case UnaryOperation.Ln:
                if (v.Sign <= 0) return Fail("Entrée invalide");
                return FromDouble(Math.Log(x), out result);

            case UnaryOperation.Log:
                if (v.Sign <= 0) return Fail("Entrée invalide");
                return FromDouble(Math.Log10(x), out result);

            case UnaryOperation.Factorial:
            {
                if (v.Sign < 0 || !v.IsInteger) return Fail("Entrée invalide");
                if (v.Num > MaxFactorial) return Fail("Résultat trop grand"); // 28! dépasse 7,9 × 10^28
                BigInteger f = BigInteger.One;
                for (int i = 2; i <= (int)v.Num; i++) f *= i;
                result = new Rational(f, BigInteger.One);
                return true;
            }

            default:
                return false;
        }
    }

    private double ToRadians(double x) => UseDegrees ? x * Math.PI / 180.0 : x;

    private double FromRadians(double x) => UseDegrees ? x * 180.0 / Math.PI : x;

    /// <summary>Convertit un double en rationnel (arrondi à 12 décimales : sin 30° donne exactement 1/2).</summary>
    private bool FromDouble(double d, out Rational result)
    {
        result = Rational.Zero;
        if (double.IsNaN(d)) return Fail("Entrée invalide");
        if (double.IsInfinity(d) || Math.Abs(d) >= (double)MaxValue) return Fail("Résultat trop grand");
        result = Rational.FromDecimal((decimal)Math.Round(d, 12));
        return true;
    }

    /// <summary>Racine carrée entière (plus grand entier r tel que r² ≤ n).</summary>
    private static BigInteger ISqrt(BigInteger n)
    {
        if (n < 2) return n;
        BigInteger x = new(Math.Sqrt((double)n));
        if (x.IsZero) x = BigInteger.One;
        while (true)
        {
            BigInteger y = (x + n / x) >> 1;
            if (BigInteger.Abs(y - x) <= 1) { x = y; break; }
            x = y;
        }
        while (x * x > n) x--;
        while ((x + 1) * (x + 1) <= n) x++;
        return x;
    }

    // ───────────────────────── État interne ─────────────────────────

    private bool CheckRange(Rational r) =>
        BigInteger.Abs(r.Num) >= MaxBig * r.Den ? Fail("Résultat trop grand") : true;

    private bool Fail(string message)
    {
        SetError(message);
        return false;
    }

    private void SetError(string message)
    {
        _error = message;
        _typing = false;
        _awaitingOperand = false;
    }

    /// <summary>Affiche une valeur calculée (la saisie est terminée).</summary>
    private void SetValue(Rational value)
    {
        _value = value;
        _typing = false;
        _awaitingOperand = false;
    }

    /// <summary>Insère une valeur comme si elle venait d'être saisie (constante, rappel mémoire).</summary>
    private void InsertValue(Rational value)
    {
        BeginEntry();
        SetValue(value);
    }

    /// <summary>Fige la valeur affichée : la prochaine saisie repart d'une nouvelle valeur.</summary>
    private void Commit()
    {
        if (_awaitingOperand) return;
        _value = Current;
        _typing = false;
    }

    private void BeginEntry()
    {
        if (_error is not null) ResetAll();
        if (_typing) return;

        if (_op is null)
        {
            // nouveau calcul après un résultat
            _expression = string.Empty;
            _left = null;
            _repeatOp = null;
            ResetAutoFraction();
        }
        else if (_left is Rational l)
        {
            _expression = $"{Fmt(l)} {_op}";
        }
        _entry = "0";
        _typing = true;
        _awaitingOperand = false;
    }

    private void ResetAll()
    {
        _entry = "0";
        _value = Rational.Zero;
        _typing = true;
        _awaitingOperand = false;
        _left = null;
        _op = null;
        _repeatOp = null;
        _repeatOperand = Rational.Zero;
        _expression = string.Empty;
        _error = null;
        ResetAutoFraction();
    }

    private void ResetAutoFraction()
    {
        if (!_autoFraction) return;
        OutputFormat = ResultFormat.Decimal;
        _autoFraction = false;
    }

    private string Prefix() =>
        _op is char o && _left is Rational l ? $"{Fmt(l)} {o} " : string.Empty;

    /// <summary>Texte d'un nombre dans l'expression : fraction si le mode fractionnaire est actif.</summary>
    private string Fmt(Rational r) =>
        OutputFormat == ResultFormat.Fraction ? FractionText(r) : Pretty(ToPlain(r));

    private static Rational ParseEntry(string entry)
    {
        int slash = entry.IndexOf('/');
        if (slash < 0) return Rational.FromDecimal(decimal.Parse(entry, Inv)); // "12.", "-0.5"

        bool negative = entry.StartsWith('-');
        string n = entry[(negative ? 1 : 0)..slash];
        string d = entry[(slash + 1)..];
        BigInteger num = n.Length == 0 ? BigInteger.Zero : BigInteger.Parse(n, Inv);
        BigInteger den = d.Length == 0 ? BigInteger.One : BigInteger.Parse(d, Inv); // "3/" vaut 3/1
        return new Rational(negative ? -num : num, den);
    }

    // ───────────────────────── Formats d'affichage ─────────────────────────

    /// <summary>Chaîne décimale invariante d'un rationnel : 10 décimales et 15 chiffres significatifs au plus.</summary>
    private static string ToPlain(Rational r)
    {
        BigInteger abs = BigInteger.Abs(r.Num);
        BigInteger intPart = abs / r.Den;
        int intDigits = intPart.IsZero ? 1 : intPart.ToString().Length;
        int decimals = Math.Clamp(MaxDigits - intDigits, 0, MaxDecimals);

        BigInteger scaled = (abs * BigInteger.Pow(10, decimals) * 2 + r.Den) / (r.Den * 2); // arrondi au plus proche
        if (scaled.IsZero) return "0";

        string digits = scaled.ToString().PadLeft(decimals + 1, '0');
        string ip = digits[..^decimals];
        string fp = digits[^decimals..].TrimEnd('0');
        return (r.Sign < 0 ? "-" : string.Empty) + ip + (fp.Length > 0 ? "." + fp : string.Empty);
    }

    /// <summary>Format décimal français : virgule, séparateur de milliers, notation scientifique si trop grand.</summary>
    private static string Pretty(string plain)
    {
        if (plain.Contains('/')) return plain; // fraction en cours de saisie

        bool negative = plain.StartsWith('-');
        string body = negative ? plain[1..] : plain;
        int dot = body.IndexOf('.');
        string intPart = dot >= 0 ? body[..dot] : body;
        string frac = dot >= 0 ? body[dot..] : string.Empty;

        if (intPart.Length > MaxDigits)
            return decimal.Parse(plain, Inv).ToString("0.#########E+0", Inv).Replace('.', ',');

        var sb = new StringBuilder();
        for (int i = 0; i < intPart.Length; i++)
        {
            if (i > 0 && (intPart.Length - i) % 3 == 0) sb.Append('\u00A0');
            sb.Append(intPart[i]);
        }
        return (negative ? "-" : string.Empty) + sb + frac.Replace('.', ',');
    }

    /// <summary>
    /// Forme rationnelle « n/d ». Si la valeur n'a pas d'écriture fractionnaire raisonnable
    /// (irrationnel arrondi, dénominateur trop grand), la valeur décimale est affichée telle quelle.
    /// </summary>
    private static string FractionText(Rational r)
    {
        if (r.IsInteger || r.Den > MaxExactDenominator) return Pretty(ToPlain(r));
        string sign = r.Sign < 0 ? "-" : string.Empty;
        return $"{sign}{BigInteger.Abs(r.Num)}/{r.Den}";
    }

    /// <summary>Écriture scientifique : 1234,5 devient « 1,2345 × 10^3 ».</summary>
    private static string ScientificText(string plain)
    {
        bool negative = plain.StartsWith('-');
        string body = negative ? plain[1..] : plain;
        int dot = body.IndexOf('.');
        string ip = (dot >= 0 ? body[..dot] : body).TrimStart('0');
        string fp = dot >= 0 ? body[(dot + 1)..] : string.Empty;

        string digits;
        int exponent;
        if (ip.Length > 0)
        {
            digits = ip + fp;
            exponent = ip.Length - 1;
        }
        else
        {
            string significant = fp.TrimStart('0');
            if (significant.Length == 0) return "0";
            exponent = -(fp.Length - significant.Length + 1);
            digits = significant;
        }

        digits = digits.TrimEnd('0');
        if (digits.Length == 0) return "0";
        string mantissa = digits.Length > 1 ? digits[0] + "," + digits[1..] : digits;
        return $"{(negative ? "-" : string.Empty)}{mantissa} × 10^{exponent}";
    }
}
