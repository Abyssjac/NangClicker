using System;
using UnityEngine;

namespace NangClicker.HexWave
{
    [Serializable]
    public struct HexCoordinate : IEquatable<HexCoordinate>, IComparable<HexCoordinate>
    {
        private static readonly HexCoordinate[] NeighborDirections =
        {
            new HexCoordinate(0, 1, -1),
            new HexCoordinate(1, 0, -1),
            new HexCoordinate(1, -1, 0),
            new HexCoordinate(0, -1, 1),
            new HexCoordinate(-1, 0, 1),
            new HexCoordinate(-1, 1, 0)
        };

        [SerializeField] private int a;
        [SerializeField] private int b;
        [SerializeField] private int c;

        public int A => a;
        public int B => b;
        public int C => c;
        public bool IsValid => a + b + c == 0;

        public HexCoordinate(int a, int b, int c)
        {
            if (a + b + c != 0)
                throw new ArgumentException("Cube coordinates must satisfy a + b + c = 0.");

            this.a = a;
            this.b = b;
            this.c = c;
        }

        public static HexCoordinate Origin => new HexCoordinate(0, 0, 0);

        public static HexCoordinate FromAxial(int a, int b)
        {
            return new HexCoordinate(a, b, -a - b);
        }

        public static HexCoordinate GetNeighborDirection(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= NeighborDirections.Length)
                throw new ArgumentOutOfRangeException(nameof(directionIndex));

            return NeighborDirections[directionIndex];
        }

        public HexCoordinate GetNeighbor(int directionIndex)
        {
            return this + GetNeighborDirection(directionIndex);
        }

        public int DistanceTo(HexCoordinate other)
        {
            int da = Mathf.Abs(a - other.a);
            int db = Mathf.Abs(b - other.b);
            int dc = Mathf.Abs(c - other.c);
            return Mathf.Max(da, Mathf.Max(db, dc));
        }

        public Vector3 ToLocalPosition(float spacing, float localY = 0f)
        {
            float x = 0.5f * Mathf.Sqrt(3f) * spacing * a;
            float z = spacing * (0.5f * a + b);
            return new Vector3(x, localY, z);
        }

        public static HexCoordinate FromLocalPosition(Vector3 localPosition, float spacing)
        {
            if (spacing <= Mathf.Epsilon)
                throw new ArgumentOutOfRangeException(nameof(spacing), "Spacing must be greater than zero.");

            float floatA = 2f * localPosition.x / (Mathf.Sqrt(3f) * spacing);
            float floatB = localPosition.z / spacing - 0.5f * floatA;
            float floatC = -floatA - floatB;

            int roundedA = Mathf.RoundToInt(floatA);
            int roundedB = Mathf.RoundToInt(floatB);
            int roundedC = Mathf.RoundToInt(floatC);

            float errorA = Mathf.Abs(roundedA - floatA);
            float errorB = Mathf.Abs(roundedB - floatB);
            float errorC = Mathf.Abs(roundedC - floatC);

            if (errorA > errorB && errorA > errorC)
                roundedA = -roundedB - roundedC;
            else if (errorB > errorC)
                roundedB = -roundedA - roundedC;
            else
                roundedC = -roundedA - roundedB;

            return new HexCoordinate(roundedA, roundedB, roundedC);
        }

        public bool Equals(HexCoordinate other)
        {
            return a == other.a && b == other.b && c == other.c;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = a;
                hashCode = (hashCode * 397) ^ b;
                hashCode = (hashCode * 397) ^ c;
                return hashCode;
            }
        }

        public int CompareTo(HexCoordinate other)
        {
            int compareA = a.CompareTo(other.a);
            if (compareA != 0)
                return compareA;

            int compareB = b.CompareTo(other.b);
            return compareB != 0 ? compareB : c.CompareTo(other.c);
        }

        public override string ToString()
        {
            return $"({a}, {b}, {c})";
        }

        public static HexCoordinate operator +(HexCoordinate left, HexCoordinate right)
        {
            return new HexCoordinate(left.a + right.a, left.b + right.b, left.c + right.c);
        }

        public static bool operator ==(HexCoordinate left, HexCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCoordinate left, HexCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
