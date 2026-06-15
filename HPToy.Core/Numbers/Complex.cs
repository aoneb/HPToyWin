namespace HPToy.Core.Numbers;

public sealed class Complex
{
    private double _real;
    private double _img;

    public Complex(double real, double img)
    {
        _real = real;
        _img = img;
    }

    public static Complex TrigonometricForm(double mod, double arg) =>
        new(mod * Math.Cos(arg), mod * Math.Sin(arg));

    public Complex() : this(0, 0)
    {
    }

    public Complex(Complex c) : this(c._real, c._img)
    {
    }

    public Complex Add(Complex c) => new(_real + c._real, _img + c._img);
    public Complex Sub(Complex c) => new(_real - c._real, _img - c._img);

    public Complex Mul(Complex c) =>
        new(_real * c._real - _img * c._img, _real * c._img + _img * c._real);

    public Complex Conj() => new(_real, -_img);

    public Complex Reciprocal()
    {
        var mod2 = Mul(Conj())._real;
        return new Complex(_real / mod2, -_img / mod2);
    }

    public Complex Div(Complex c) => Mul(c.Reciprocal());

    public double Mod() => Math.Sqrt(_real * _real + _img * _img);

    public override string ToString() => $"real={_real}, img={_img}";
}
