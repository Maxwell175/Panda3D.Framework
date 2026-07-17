using Panda3D.Core;
using Xunit;

namespace Panda3D.Framework.Core.Tests;

public sealed class InteropMathConvenienceTests
{
    [Fact]
    public void GeneratedVectorTypesSupportIndexersAndDeconstruction()
    {
        using var v2 = new LVecBase2d(1.25, 2.5);
        var (x2, y2) = v2;

        Assert.Equal(1.25, x2);
        Assert.Equal(2.5, y2);

        v2[0] = 8.5;
        Assert.Equal(8.5, v2.X);

        using var v3 = new LVector3f(1, 2, 3);
        var (x3, y3, z3) = v3;

        Assert.Equal(1, x3);
        Assert.Equal(2, y3);
        Assert.Equal(3, z3);

        v3[1] = 7;
        Assert.Equal(7, v3.Y);

        using var v4 = new LVecBase4i(4, 5, 6, 7);
        var (x4, y4, z4, w4) = v4;

        Assert.Equal(4, x4);
        Assert.Equal(5, y4);
        Assert.Equal(6, z4);
        Assert.Equal(7, w4);

        v4[3] = 11;
        Assert.Equal(11, v4.GetCell(3));
    }

    [Fact]
    public void GeneratedMatrixTypesSupportIndexersAndRowDeconstruction()
    {
        using var m3 = new LMatrix3d(
            1, 2, 3,
            4, 5, 6,
            7, 8, 9);

        Assert.Equal(8, m3[2, 1]);

        m3[0, 2] = 13;
        Assert.Equal(13, m3.GetCell(0, 2));

        var (m3r0, m3r1, m3r2) = m3;
        using (m3r0)
        using (m3r1)
        using (m3r2)
        {
            var (_, _, row0z) = m3r0;
            var (_, row2y, _) = m3r2;

            Assert.Equal(13, row0z);
            Assert.Equal(8, row2y);
        }

        using var m4 = new LMatrix4f(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        Assert.Equal(12, m4[2, 3]);

        m4[2, 3] = 99;
        Assert.Equal(99, m4.GetCell(2, 3));

        var (m4r0, m4r1, m4r2, m4r3) = m4;
        using (m4r0)
        using (m4r1)
        using (m4r2)
        using (m4r3)
        {
            var (_, _, _, row2w) = m4r2;
            Assert.Equal(99, row2w);
        }
    }
}
