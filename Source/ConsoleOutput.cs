﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuakeConsole.Utilities;
#if MONOGAME
using Microsoft.Xna.Framework;
#endif

namespace QuakeConsole
{
    /// <summary>
    /// Output part of the <see cref="Console"/>. Command execution info will be appended here.
    /// </summary>
    internal class ConsoleOutput : IConsoleOutput
    {
        private const string MeasureFontSizeSymbol = "x";
        
        private readonly CircularArray<OutputEntry> _entries = new CircularArray<OutputEntry>();
        private readonly List<OutputEntry> _commandEntries = new List<OutputEntry>();        
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        private Pool<OutputEntry> _entryPool;

        private Vector2 _fontSize;
        private int _maxNumRows;
        private int _numRows;           
        private bool _removeOverflownEntries;        

        internal void LoadContent(Console console)
        {
            Console = console;
            _entryPool = new Pool<OutputEntry>(() => new OutputEntry(this));

            // TODO: Set flags only and do any calculation in Update. While this would win in performance
            // TODO: in some cases, I'm not convinced it's worth the hit against readability.
            Console.PaddingChanged += (s, e) =>
            {
                CalculateRows();
                RemoveOverflownBufferEntriesIfAllowed();
            };
            Console.FontChanged += (s, e) =>
            {
                CalculateFontSize();
                CalculateRows();
                RemoveOverflownBufferEntriesIfAllowed();
            };
            Console.WindowAreaChanged += (s, e) =>
            {
                CalculateRows();
                RemoveOverflownBufferEntriesIfAllowed();
            };

            CalculateFontSize();
            CalculateRows();
        }
        
        public bool RemoveOverflownEntries 
        {
            get { return _removeOverflownEntries; }
            set 
            { 
                _removeOverflownEntries = value;
                RemoveOverflownBufferEntriesIfAllowed();
            }
        }

        internal Console Console { get; private set; }

        internal bool HasCommandEntry => _commandEntries.Count > 0;

        /// <summary>
        /// Appends a message to the buffer.
        /// </summary>
        /// <param name="message">Message to append.</param>
        public void Append(string message)
        {
            if (message == null) return;            

            var viewBufferEntry = _entryPool.Fetch();
            viewBufferEntry.Value = message;            
            _numRows += viewBufferEntry.CalculateLines(Console.WindowArea.Width - Console.Padding * 2, false);
            _entries.Enqueue(viewBufferEntry);
            RemoveOverflownBufferEntriesIfAllowed();
        }

        /// <summary>
        /// Clears all the information in the buffer.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _commandEntries.Clear();
        }

        internal void AddCommandEntry(string value)
        {
            if (value == null) return;

            var entry = _entryPool.Fetch();
            entry.Value = value;
            _numRows++;
            //entry.CalculateLines(_console.WindowArea.Width - _console.Padding * 2, true);
            _commandEntries.Add(entry);
        }

        internal string DequeueCommandEntry()
        {
            _stringBuilder.Clear();
            for (int i = 0; i < _commandEntries.Count; i++)
            {
                _stringBuilder.Append(_commandEntries[i].Value);
                //if (i != _commandEntries.Count - 1)
                _stringBuilder.Append("\n");
            }
            _commandEntries.Clear();
            return _stringBuilder.ToString();
        }        

        internal void Draw()
        {
            // Draw from bottom to top.
            var viewPosition = new Vector2(
                Console.Padding, 
                Console.WindowArea.Y + Console.WindowArea.Height - Console.Padding - Console.ConsoleInput.InputPrefixSize.Y - _fontSize.Y);

            int rowCounter = 0;

            for (int i = _commandEntries.Count - 1; i >= 0; i--)
            {
                if (rowCounter >= _maxNumRows) return;
                DrawRow(_commandEntries[i], ref viewPosition, ref rowCounter, true);
            }

            for (int i = _entries.Length - 1; i >= 0; i--)
            {
                if (rowCounter >= _maxNumRows) return;
                DrawRow(_entries[i], ref viewPosition, ref rowCounter, false);
            }
        }

        internal void SetDefaults(ConsoleSettings settings)
        {
        }

        private void DrawRow(OutputEntry entry, ref Vector2 viewPosition, ref int rowCounter, bool drawPrefix)
        {            
            for (int j = entry.Lines.Count - 1; j >= 0; j--)
            {
                Vector2 tempViewPos = viewPosition;
                if (drawPrefix)
                {
                    Console.SpriteBatch.DrawString(
                        Console.Font,
                        Console.ConsoleInput.InputPrefix,
                        tempViewPos,
                        Console.ConsoleInput.InputPrefixColor);
                    tempViewPos.X += Console.ConsoleInput.InputPrefixSize.X;
                }
                Console.SpriteBatch.DrawString(
                    Console.Font,
                    entry.Lines[j],
                    tempViewPos, 
                    Console.FontColor);
                viewPosition.Y -= _fontSize.Y;
                rowCounter++;
            }
        }

        private void RemoveOverflownBufferEntriesIfAllowed()
        {
            if (!RemoveOverflownEntries) return;
            
            while (_numRows > _maxNumRows)
            {
                OutputEntry entry = _entries.Peek();

                // Remove entry only if it is completely hidden from view.
                if (_numRows - entry.Lines.Count >= _maxNumRows)
                {
                    _numRows -= entry.Lines.Count;
                    _entries.Dequeue();
                    _entryPool.Release(entry);
                }
                else
                {
                    break;
                }
            }
        }

        private void CalculateRows()
        {
            // Take top padding into account and hide any row which is only partly visible.
            //_maxNumRows = Math.Max((int)((_console.WindowArea.Height - _console.Padding * 2) / _fontSize.Y) - 1, 0);            

            // Disregard top padding and allow any row which is only partly visible.
            _maxNumRows = Math.Max((int)Math.Ceiling(((Console.WindowArea.Height - Console.Padding) / _fontSize.Y)) - 1, 0);
            
            _numRows = _commandEntries.Count + /*GetNumRows(_commandEntries) +*/ GetNumRows(_entries);
        }

        private int GetNumRows(IEnumerable<OutputEntry> collection)
        {
            return collection.Sum(entry => entry.CalculateLines(Console.WindowArea.Width - Console.Padding * 2, false));
        }

        private void CalculateFontSize()
        {
            _fontSize = Console.Font.MeasureString(MeasureFontSizeSymbol);
        }        
    }
}
