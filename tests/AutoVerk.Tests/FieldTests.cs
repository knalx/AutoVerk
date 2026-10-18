using System;
using AutoVerk.Skills.Farm;
using Xunit;

namespace AutoVerk.Tests
{
    public class FieldTests
    {
        private static Field Facing(double fx, double fz, bool staggered = false) => new Field
        {
            Name = "carrots-1", Seed = "CarrotSeeds", X = 10, Y = 30, Z = 20,
            ForwardX = fx, ForwardZ = fz, Count = 5, LineLength = 2, Spacing = 0.7, Staggered = staggered,
        };

        [Fact]
        public void SpotsGoForwardThenBackOneLineToTheRight()
        {
            var spots = Facing(0, 1).Spots();
            Assert.Equal(5, spots.Count);
            // Line 0 starts a metre ahead and goes forward (+z).
            Assert.Equal(10, spots[0].X, 3);
            Assert.Equal(21, spots[0].Z, 3);
            Assert.Equal(21.7, spots[1].Z, 3);
            Assert.Equal(1, spots[0].PathZ, 3);
            // Line 1 is one spacing to the right (+x when facing +z) and comes back.
            Assert.Equal(10.7, spots[2].X, 3);
            Assert.Equal(21.7, spots[2].Z, 3);
            Assert.Equal(21, spots[3].Z, 3);
            Assert.Equal(-1, spots[2].PathZ, 3);
            // Line 2 goes forward again.
            Assert.Equal(11.4, spots[4].X, 3);
            Assert.Equal(21, spots[4].Z, 3);
        }

        [Fact]
        public void StaggeredLinesAreCloserAndShiftedHalfASpot()
        {
            var spots = Facing(0, 1, staggered: true).Spots();
            Assert.Equal(10 + 0.7 * Math.Sqrt(3) / 2, spots[2].X, 3);
            Assert.Equal(21.7 + 0.35, spots[2].Z, 3);
            // Every neighbour is one spacing away.
            double dx = spots[2].X - spots[1].X, dz = spots[2].Z - spots[1].Z;
            Assert.Equal(0.7, Math.Sqrt(dx * dx + dz * dz), 3);
        }

        [Fact]
        public void ALineRoundTrips()
        {
            var field = Facing(0.6, 0.8, staggered: true);
            var back = Field.FromLine(field.ToLine());
            Assert.NotNull(back);
            Assert.Equal(field.ToLine(), back.ToLine());
            Assert.True(back.Staggered);
            Assert.Equal(0.8, back.ForwardZ, 3);
        }

        [Fact]
        public void BadLinesAreLeftOut()
        {
            var book = FieldBook.FromText(Facing(0, 1).ToLine() + "\nfield name=broken\nnot a field\n\n");
            Assert.Single(book.Fields);
            Assert.NotNull(book.Find("CARROTS-1"));
        }

        [Fact]
        public void NewNamesCountUp()
        {
            var book = new FieldBook();
            Assert.Equal("carrots-1", book.NextName("Carrot"));
            book.Fields.Add(Facing(0, 1));
            var third = Facing(0, 1);
            third.Name = "carrots-3";
            book.Fields.Add(third);
            Assert.Equal("carrots-4", book.NextName("Carrot"));
            Assert.Equal("turnips-1", book.NextName("Turnips"));
        }
    }
}
