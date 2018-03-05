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
using System.IO;
using Pegasus.Common;
using Pegasus.Common.Tracing;
using DOLE;

namespace PuzzLangLib {
  /// <summary>
  /// Extensions to the generated parser
  /// </summary>
  partial class PuzzLangParser {
    ParseManager _manager { get; set; }
    internal int LineNumber { get { return _prevline; } }
    int _lastline = 1; // line number after printing
    int _prevline = 1; // line number before printing (more useful)
    int _lastloc = 0;

    internal static PuzzLangParser Create(TextWriter tw, ParseManager manager) {
      return new PuzzLangParser {
        Tracer = new Tracer { Out = tw },
        _manager = manager,
      };
    }

    string Eol(Cursor state) {
      if (state.Location > _lastloc) {
        _prevline = _lastline;
        var lines = state.Subject.Substring(_lastloc, state.Location - _lastloc).Split('\n');
        foreach (var line in lines.Take(lines.Length - 1)) {
          if (Logger.Level >= 2)
            Logger.WriteLine("{0,4} : {1}", _lastline, line.Trim());
          _lastline++;
        }
        _lastloc = state.Location;
      }
      return null;
    }

    bool IsSetting(string value) {
      if (value == null || value.EndsWith("_")) return false;
      return value.SafeEnumParse<OptionSetting>() != null;
    }

    string DefSetting(Cursor cursor, string name, string value) {
      if (!_manager.SettingsParser.Parse(name, value)) ParseError(cursor, $"invalid setting '{name}' value '{value}'");
      Logger.WriteLine(2, $"Setting {name}='{value}'");
      return null;
    }

    string DefObject(Cursor cursor, string ident, string glyph, IList<string> colours, IList<string> gridlines) {
      var badcolours = colours
        .Where(c => !_manager.ColourParser.IsColour(c))
        .ToList();
      if (badcolours.Count > 0)
        ParseError(cursor, $"colour(s) '{badcolours.Join()}' not valid");
      var ctable = colours.Select(c => _manager.ColourParser.ParseColour(c)).ToList();
      if (gridlines.Count == 0) {
        _manager.AddObject(ident, 1, new int[] { ctable[0] });
      } else {
        if (!(gridlines.All(s => s.Length == gridlines.Count)))
          ParseError(cursor, $"sprite not square");
        var maxchar = '0' + colours.Count - 1;
        var badchar = gridlines.Join("")
          .Where(c => c > maxchar).ToList();
        if (badchar.Count > 0)
          ParseError(cursor, $"sprite char(s) '{badchar.Join()}' not valid");
        var grid = gridlines.SelectMany(s => s.Select(c => c == '.' ? -1 : ctable[c - '0'])).ToList();
        _manager.AddObject(ident, gridlines.Count, grid);
      }
      if (glyph != null) _manager.AddObjectDef(glyph, new List<string> { ident }, LogicOperator.None);
      return null;
    }

    bool IsColour(string value) {
      return _manager.ColourParser.IsColour(value);
    }
    bool IsObject(string value) {
      return _manager.ParseSymbol(value).Kind == SymbolKind.Alias;
    }
    bool IsDefined(string value) {
      return _manager.ParseSymbol(value) != null;
    }
    bool IsWinAction(string value) {
      return value.SafeEnumParse<MatchOperator>() != null;
    }
    bool IsSoundTrigger(string value) {
      return _manager.ParseTriggerEvent(value) != SoundTrigger.None;
    }
    bool IsRuleCommand(string value) {
      return _manager.ParseRuleCommand(value) != RuleCommand.None;
    }
    bool IsRulePrefix(string value) {
      return _manager.ParseRulePrefix(value) != RulePrefix.None;
    }
    bool IsRuleDirection(string value) {
      var dir = _manager.ParseDirection(value);
      return dir != Direction.None && dir <= Direction.Orthogonal;
    }
    bool IsMoveDirection(string value) {
      var dir = _manager.ParseDirection(value);
      return dir != Direction.None; ;
    }

    string DefLegend(string ident, IList<string> objects, string andor) {
      var op = (objects.Count == 1) ? LogicOperator.None
        : andor.SafeEnumParse<LogicOperator>() ?? LogicOperator.None;
      if (IsDefined(ident))
        _manager.CompileWarn("'{0}' already defined", ident);
      else {
        _manager.AddObjectDef(ident, objects, op);
      }
      return null;
    }

    // add sound trigger
    string DefSound(string objct, string trigger, IList<string> directions, string seed) {
      _manager.AddSound(trigger, seed, objct, directions);
      return null;
    }
    string DefCollision(Cursor cursor, IList<string> idents) {
      _manager.AddLayer(idents);
      return null;
    }
    string Loop(Cursor state, string loop) {
      if (!_manager.AddLoop(loop.ToLower() == "startloop" ? RulePrefix.Startloop_ : RulePrefix.Endloop_))
        ParseError(state, $"nesting error: {loop}");
      return null;
    }
    string DefRule(Cursor state, IList<string> prefordirs,
      IList<SubRule> pattern, IList<SubRule> action, IList<string> commands) {

      if (action.Count == 0) {
        if (action == null) ParseError(state, "rule missing action or command");
      } else {
        var ok = (pattern.Count == action.Count)
          && Enumerable.Range(0, pattern.Count).All(x => pattern[x].Cells.Count == action[x].Cells.Count);
        if (!ok) ParseError(state, "rule pattern and action size mismatch");
      }
      var prefixes = prefordirs.Where(p => IsRulePrefix(p)).ToList();
      var directions = prefordirs.Where(p => !IsRulePrefix(p)).ToList();
      _manager.AddRule(LineNumber, prefixes, directions, pattern, action, commands);
      return null;
    }

    // define a rule sequence (pattern or action) as a list of cells
    SubRule DefSubRule(IList<string> directions, IList<IList<RuleAtom>> subparts) {
      return new SubRule {
        Cells = subparts
      };
    }
    
    // define a cell as a special value
    IList<RuleAtom> DefRuleCell(string arg) {
      return new List<RuleAtom> {
        _manager.CreateRuleCell(null, null, arg, null)
      };
    }
    
    // define a cell as a list of matches
    RuleAtom DefRuleCell(string noflag, string direction, string ident, string command) {
      return _manager.CreateRuleCell(noflag, direction, ident, command);
    }

    string DefWinCondition(string condition, string ident, string other) {
      _manager.AddWin(condition, ident, other);
      Logger.WriteLine(2, $">Win {condition},{ident},{other}");
      return null;
    }

    // parse and define a game level
    string DefLevel(Cursor cursor, IList<string> lines) {
      var bad = lines.Join("").Where(c => !IsDefined(new String(c, 1))).ToList();
      if (bad.Count > 0)
        ParseError(cursor, $"level char '{bad.Join("")}' not valid");
      _manager.AddLevel(lines);
      return null;
    }

    // parse and define a message
    string DefMessage(string message) {
      _manager.AddMessage(message);
      return null;
    }

    void ParseError(Cursor cursor, string format, params string[] args) {
      throw ExceptionHelper(cursor, state => String.Format(format, args));
    }

    T Single<T>(IList<T> list) where T : class {
      return list.Count > 0 ? list[0] : null;
    }

    List<T> ListOf<T>(params T[] args) {
      return args.ToList();
    }
  }

  /// <summary>
  /// 
  /// </summary>
  internal class Tracer : ITracer {
    internal TextWriter Out { get; set; }
    internal bool Enabled { get; set; }

    public void TraceCacheHit<T>(string ruleName, Cursor cursor, CacheKey cacheKey, IParseResult<T> parseResult) {
    }

    public void TraceCacheMiss(string ruleName, Cursor cursor, CacheKey cacheKey) {
    }

    public void TraceInfo(string ruleName, Cursor cursor, string info) {
      if (Enabled) Out.WriteLine($"Info rule: {ruleName} info: {info}");
    }

    public void TraceRuleEnter(string ruleName, Cursor cursor) {
      if (Enabled) Out.WriteLine($"Enter rule: {ruleName}");
    }

    public void TraceRuleExit<T>(string ruleName, Cursor cursor, IParseResult<T> parseResult) {
      if (Enabled) Out.WriteLine($"Exit rule: {ruleName} result: {parseResult}");
    }
  }
}