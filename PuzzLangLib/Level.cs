﻿/// Puzzlang is a pattern matching language for abstract games and puzzles. See http://www.polyomino.com/puzzlang.
///
/// Copyright © Polyomino Games 2018. All rights reserved.
/// 
/// This is free software. You are free to use it, modify it and/or 
/// distribute it as set out in the licence at http://www.polyomino.com/licence.
/// You should have received a copy of the licence with the software.
/// 
/// This software is distributed in the hope that it will be useful, but with
/// absolutely no warranty, express or implied. See the licence for details.
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using DOLE;

namespace PuzzLangLib {
  /// <summary>
  /// specific location of an object in a level
  /// </summary>
  struct Locator {
    internal int Index;
    internal int Layer;
    internal static Locator Null = Create(-1, -1);
    internal bool IsNull { get { return Index == -1; } }

    static internal Locator Create(int index, int layer) {
      return new Locator { Index = index, Layer = layer };
    }
    public override string ToString() {
      return $"[{Index}:{Layer}]";
    }
  }

  /// <summary>
  /// Layout of current location of objects
  /// Also tracks changes
  /// </summary>
  public class Level {
    public string Name { get; private set; }
    public int Width { get; private set; }
    public int Length { get; private set; }
    public int Height { get; private set; }
    public int Depth { get; private set; }
    //public int Length { get { return _locations.GetLength(0); } }
    //public int Height { get { return Length / Width; } }
    //public int Depth { get { return _locations.GetLength(1); } }
    internal int[,] Locations { get { return _locations; } }

    internal int this[int x, int y, int layer] {
      get { return _locations[GetCellIndex(x, y), layer - 1]; }
      set { _locations[GetCellIndex(x, y), layer - 1] = value; }
    }
    public int this[int index, int layer] {
      get { return _locations[index, layer - 1]; }
      set { _locations[index, layer - 1] = value; }
    }
    internal int this[Locator locator] {
      get { return _locations[locator.Index, locator.Layer - 1]; }
    }
    int GetCellIndex(int x, int y) {
      //if (!IsCellCoords(x, y)) throw Error.Assert("out of range");
      return y * Width + x;
    }
    bool IsCellCoords(int x, int y) {
      return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    int[,] _locations;

    // lookup increment for x, y
    static readonly Dictionary<Direction, Pair<int, int>> _steplookup = new Dictionary<Direction, Pair<int, int>> {
      { Direction.Up, Pair.Create(0,-1) },
      { Direction.Right, Pair.Create(1,0) },
      { Direction.Down, Pair.Create(0,1) },
      { Direction.Left, Pair.Create(-1,0) },
    };

    //--- ctor
    static internal Level Create(int x, int y, int z, string name) {
      return new Level {
        Name = name,
        Width = x,
        Height = y,
        Depth = z,
        Length = x * y,
        _locations = new int[x * y, z],
      };
    }

    // Create new level as clone of this one
    internal Level Clone(string name = null) {
      return new Level {
        Name = name ?? Name,
        _locations = _locations.Clone() as int[,],
        Width = Width,
        Height = Height,
        Depth = Depth,
        Length = Length,
      };
    }

    // return list of objects found at this location (for testing and background)
    internal IList<int> GetObjects(int cellindex) {
      return Enumerable.Range(1, Depth).Select(z => this[cellindex, z])
        .Where(v => v != 0)
        .ToList();
    }

    // Try to step a level index by a direction
    internal int? Step(int cellindex, Direction direction) {
      //if (!GameDef.MoveDirections.Contains(direction)) throw Error.Assert("dir: {0}", direction);
      if (!_steplookup.ContainsKey(direction)) return cellindex;
      var x = cellindex % Width + _steplookup[direction].Item1;
      var y = cellindex / Width + _steplookup[direction].Item2;
      if (!IsCellCoords(x, y)) return null;
      return GetCellIndex(x, y);
    }
  }
}
