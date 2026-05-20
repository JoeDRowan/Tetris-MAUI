namespace Tetris.Models;

using Tetris.Config;

/// <summary>
/// Represents a tetromino piece with its shape data and rotation states.
/// </summary>
public class Tetromino
{
    public TetrominoShape Shape { get; }
    public int RotationState { get; set; }

    private readonly int[][,] _rotations;

    public Tetromino(TetrominoShape shape)
    {
        Shape = shape;
        RotationState = 0;
        _rotations = TetrominoData.GetRotations(shape);
    }

    public int[,] CurrentMatrix => _rotations[RotationState];
    public int RotationCount => _rotations.Length;

    public int[,] GetRotation(int state) => _rotations[((state % RotationCount) + RotationCount) % RotationCount];

    public void RotateClockwise() => RotationState = (RotationState + 1) % RotationCount;
    public void RotateCounterClockwise() => RotationState = (RotationState - 1 + RotationCount) % RotationCount;

    public Tetromino Clone()
    {
        var clone = new Tetromino(Shape);
        clone.RotationState = RotationState;
        return clone;
    }
}

/// <summary>
/// Static definitions of all tetromino shapes and their rotation matrices.
/// Each matrix uses 1 to indicate a filled cell.
/// </summary>
public static class TetrominoData
{
    public static int[][,] GetRotations(TetrominoShape shape) => shape switch
    {
        TetrominoShape.I => RotationsI,
        TetrominoShape.O => RotationsO,
        TetrominoShape.T => RotationsT,
        TetrominoShape.S => RotationsS,
        TetrominoShape.Z => RotationsZ,
        TetrominoShape.J => RotationsJ,
        TetrominoShape.L => RotationsL,
        _ => throw new ArgumentException($"Unknown shape: {shape}")
    };

    private static readonly int[][,] RotationsI =
    [
        new int[,] { {0,0,0,0}, {1,1,1,1}, {0,0,0,0}, {0,0,0,0} },
        new int[,] { {0,0,1,0}, {0,0,1,0}, {0,0,1,0}, {0,0,1,0} },
        new int[,] { {0,0,0,0}, {0,0,0,0}, {1,1,1,1}, {0,0,0,0} },
        new int[,] { {0,1,0,0}, {0,1,0,0}, {0,1,0,0}, {0,1,0,0} },
    ];

    private static readonly int[][,] RotationsO =
    [
        new int[,] { {1,1}, {1,1} },
    ];

    private static readonly int[][,] RotationsT =
    [
        new int[,] { {0,1,0}, {1,1,1}, {0,0,0} },
        new int[,] { {0,1,0}, {0,1,1}, {0,1,0} },
        new int[,] { {0,0,0}, {1,1,1}, {0,1,0} },
        new int[,] { {0,1,0}, {1,1,0}, {0,1,0} },
    ];

    private static readonly int[][,] RotationsS =
    [
        new int[,] { {0,1,1}, {1,1,0}, {0,0,0} },
        new int[,] { {0,1,0}, {0,1,1}, {0,0,1} },
        new int[,] { {0,0,0}, {0,1,1}, {1,1,0} },
        new int[,] { {1,0,0}, {1,1,0}, {0,1,0} },
    ];

    private static readonly int[][,] RotationsZ =
    [
        new int[,] { {1,1,0}, {0,1,1}, {0,0,0} },
        new int[,] { {0,0,1}, {0,1,1}, {0,1,0} },
        new int[,] { {0,0,0}, {1,1,0}, {0,1,1} },
        new int[,] { {0,1,0}, {1,1,0}, {1,0,0} },
    ];

    private static readonly int[][,] RotationsJ =
    [
        new int[,] { {1,0,0}, {1,1,1}, {0,0,0} },
        new int[,] { {0,1,1}, {0,1,0}, {0,1,0} },
        new int[,] { {0,0,0}, {1,1,1}, {0,0,1} },
        new int[,] { {0,1,0}, {0,1,0}, {1,1,0} },
    ];

    private static readonly int[][,] RotationsL =
    [
        new int[,] { {0,0,1}, {1,1,1}, {0,0,0} },
        new int[,] { {0,1,0}, {0,1,0}, {0,1,1} },
        new int[,] { {0,0,0}, {1,1,1}, {1,0,0} },
        new int[,] { {1,1,0}, {0,1,0}, {0,1,0} },
    ];

    /// <summary>
    /// Wall kick offsets for standard pieces (not I-piece). 
    /// Indexed by [fromRotation][testIndex] = (dx, dy).
    /// </summary>
    public static readonly (int dx, int dy)[][] WallKicksCW =
    [
        // 0 -> 1
        [(0,0), (-1,0), (-1,1), (0,-2), (-1,-2)],
        // 1 -> 2
        [(0,0), (1,0), (1,-1), (0,2), (1,2)],
        // 2 -> 3
        [(0,0), (1,0), (1,1), (0,-2), (1,-2)],
        // 3 -> 0
        [(0,0), (-1,0), (-1,-1), (0,2), (-1,2)],
    ];

    public static readonly (int dx, int dy)[][] WallKicksCCW =
    [
        // 0 -> 3
        [(0,0), (1,0), (1,1), (0,-2), (1,-2)],
        // 1 -> 0
        [(0,0), (1,0), (1,-1), (0,2), (1,2)],
        // 2 -> 1
        [(0,0), (-1,0), (-1,1), (0,-2), (-1,-2)],
        // 3 -> 2
        [(0,0), (-1,0), (-1,-1), (0,2), (-1,2)],
    ];

    /// <summary>
    /// Wall kick offsets for the I-piece.
    /// </summary>
    public static readonly (int dx, int dy)[][] WallKicksICW =
    [
        // 0 -> 1
        [(0,0), (-2,0), (1,0), (-2,-1), (1,2)],
        // 1 -> 2
        [(0,0), (-1,0), (2,0), (-1,2), (2,-1)],
        // 2 -> 3
        [(0,0), (2,0), (-1,0), (2,1), (-1,-2)],
        // 3 -> 0
        [(0,0), (1,0), (-2,0), (1,-2), (-2,1)],
    ];

    public static readonly (int dx, int dy)[][] WallKicksICCW =
    [
        // 0 -> 3
        [(0,0), (-1,0), (2,0), (-1,2), (2,-1)],
        // 1 -> 0
        [(0,0), (2,0), (-1,0), (2,1), (-1,-2)],
        // 2 -> 1
        [(0,0), (1,0), (-2,0), (1,-2), (-2,1)],
        // 3 -> 2
        [(0,0), (-2,0), (1,0), (-2,-1), (1,2)],
    ];
}
