﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace TopologyChess
{
    public class Game : INotifyPropertyChanged
    {
        public Game()
        {
            DefaultSetup();
            PossibleMoves = CalculateMoves();
        }

        private Board _board;
        public Board Board
        {
            get => _board;
            set
            {
                _board = value;
                OnPropertyChanged();
            }
        }

        public Topology CurrentTopology { get; set; } = Topology.Topologies.FirstOrDefault(t => t.Name == "Flat");

        private Move LastMove { get; set; }

        public ObservableCollection<Move> History { get; set; } = new ObservableCollection<Move>();

        private Dictionary<Party, Player> Players { get; } = new()
        {
            { Party.White, new Player(Party.White) },
            { Party.Black, new Player(Party.Black) }
        };

        private Party _currentParty = Party.White;
        public Party CurrentParty
        {
            get => _currentParty;
            set
            {
                _currentParty = value;
                OnPropertyChanged();
            }
        }

        public Player CurrentPlayer => Players[CurrentParty];

        public bool IsOver { get; set; } = false;

        public void AddPiece(PieceValue value, Party color, int x, int y)
        {
            Piece piece = new Piece(value, color);
            Players[color].Add(piece);
            Board[x, y].Piece = piece;
        }

        public void DefaultSetup()
        {
            Board = new Board(8);
            for (int c = -1; c <= 1; c += 2)
            {
                Party color = (Party)c;
                int rank = c == -1 ? 0 : 7;

                AddPiece(PieceValue.Rook, color, 0, rank);
                AddPiece(PieceValue.Knight, color, 1, rank);
                AddPiece(PieceValue.Bishop, color, 2, rank);
                AddPiece(PieceValue.Queen, color, 3, rank);
                AddPiece(PieceValue.King, color, 4, rank);
                AddPiece(PieceValue.Bishop, color, 5, rank);
                AddPiece(PieceValue.Knight, color, 6, rank);
                AddPiece(PieceValue.Rook, color, 7, rank);
                for (int f = 0; f < 8; f++)
                {
                    AddPiece(PieceValue.Pawn, color, f, rank - c);
                }
            }
        }

        private bool IsAttacked(IntVector position, Move after = null)
        {
            after ??= Move.NoMove;
            Party color = (Party)(-(int)CurrentParty);
            foreach ((IntVector dir, bool slide) in Players[color].AttackDirections)
            {
                int distance = 1;
                List<Step> stepleads = new List<Step>();
                List<Step> new_stepleads = new List<Step>();
                stepleads.Add(new Step(position, -dir, Matrix.Identity));
                do
                {
                    foreach (var lead in stepleads)
                    {
                        Step next = lead;
                        next.P += next.V;
                        foreach (Step warp in CurrentTopology.Warp(next, Board.Size))
                        {
                            new_stepleads.Add(warp);
                        }
                    }
                    stepleads.Clear();
                    foreach (var lead in new_stepleads)
                    {
                        Piece piece = Board[lead.P].Piece;
                        if (piece.Color == color &&
                            piece.AttackDirections.Contains(-lead.V) &&
                            ((distance == 1) || piece.Slides) &&
                            piece.Position != after.Capture) return true;
                        if (lead.P == after.To) continue;
                        if (piece.Type == PieceType.Empty || lead.P == after.From || lead.P == after.Capture)
                            stepleads.Add(lead);
                    }
                    new_stepleads.Clear();
                } while (slide && stepleads.Any() && distance++ < Board.Size * Board.Size);
            }
            return false;
        }

        public List<Move> PieceMoves(Piece piece)
        {
            List<Move> moves = new List<Move>();
            if (piece.Type == PieceType.Empty) return moves;
            Cell from = Board[piece.Position];
            Piece king = Players[piece.Color].Pieces.FirstOrDefault(p => p.Value == PieceValue.King);
            if (piece.Value != PieceValue.Pawn)
            {
                int distance = 0;
                List<Chain<Step>> leads = new List<Chain<Step>>();
                List<Chain<Step>> new_leads = new List<Chain<Step>>();
                foreach (var d in piece.MoveDirections) leads.Add(
                    new Chain<Step>(new Step(piece.Position, d, Matrix.Identity))
                );
                do
                {
                    leads.Sort((a, b) =>
                        Topology.Sides(a.Value.P, Board.Size).Count -
                        Topology.Sides(b.Value.P, Board.Size).Count
                    );
                    foreach (var lead in leads)
                    {
                        Step next = lead.Value;
                        next.P += next.V;
                        foreach (Step warp in CurrentTopology.Warp(next, Board.Size))
                        {
                            if (!new_leads.Any(l => l.Value == warp)) new_leads.Add(lead.Add(warp));
                        }
                    }
                    leads.Clear();
                    foreach (var lead in new_leads)
                    {
                        Cell to = Board[lead.Value.P];
                        if (to.Piece.Color == piece.Color) continue;
                        if (to.Piece.Type == PieceType.Empty) leads.Add(lead);
                        if (!moves.Any(m => m.To == to.Position))
                        {
                            Move move = new Move(from, to, lead);
                            if (!IsAttacked(piece == king ? to.Position : king.Position, move))
                                moves.Add(move);
                        }
                    }
                    new_leads.Clear();
                } while (piece.Slides && leads.Any() && distance++ < Board.Size * Board.Size);
            }
            else
            {
                Piece pawn = piece;
                Chain<Step> push = new Chain<Step>(
                    new Step(pawn.Position, pawn.MoveDirections[0], Matrix.Identity)
                );
                for (int i = 0; i < (pawn.HasMoved ? 1 : 2); i++)
                {
                    Step next = push.Value;
                    next.P += next.V;
                    foreach (Step warp in CurrentTopology.Warp(next, Board.Size))
                    {
                        push = push.Add(warp);
                        Cell to = Board[push.Value.P];
                        if (to.Piece.Type == PieceType.Empty && !moves.Any(m => m.To == to.Position))
                        {
                            Move move = new Move(from, to, push);
                            if (!IsAttacked(king.Position, move))
                                moves.Add(move);
                        }
                    }
                }
                Cell enPassant = null;
                if (LastMove != null &&
                    LastMove.MovingPiece.Value == PieceValue.Pawn &&
                    LastMove.MovingPiece.Color != pawn.Color &&
                    LastMove.Path.Count() == 3)
                {
                    enPassant = Board[LastMove.Path.Previous.Value.P];
                }
                for (int i = 1; i <= 2; i++)
                {
                    Chain<Step> take = new Chain<Step>(
                        new Step(pawn.Position, pawn.MoveDirections[i], Matrix.Identity)
                    );
                    Step next = take.Value;
                    next.P += next.V;
                    foreach (Step warp in CurrentTopology.Warp(next, Board.Size))
                    {
                        Chain<Step> take1 = take.Add(warp);
                        Cell to = Board[take1.Value.P];
                        if (to.Piece.Color != pawn.Color && (to.Piece.Color != Party.None || to == enPassant))
                        {
                            Move takemove = new Move(from, to, take1);
                            if (to == enPassant) takemove.Capture = LastMove.To;
                            if (!moves.Any(m => m.To == to.Position))
                            {
                                if (!IsAttacked(king.Position, takemove))
                                    moves.Add(takemove);
                            }
                        }
                    }
                }
            }
            if (piece == king)
            {
                if (!king.HasMoved && !IsAttacked(king.Position) &&
                    Players[CurrentParty].Pieces.Any(p => p.Value == PieceValue.Rook && !p.HasMoved)
                )
                {
                    IntVector[] directions = new IntVector[4] { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };
                    foreach (var dir in directions)
                    {
                        int distance = 1;
                        List<Chain<Step>> leads = new List<Chain<Step>>();
                        List<Chain<Step>> new_leads = new List<Chain<Step>>();
                        Piece rook = null;
                        stepleads.Add(new Step(king.Position, dir, Matrix.Identity));
                        do
                        {
                            foreach (var lead in leads)
                            {
                                Step next = lead.Value;
                                next.P += next.V;
                                foreach (Step warp in CurrentTopology.Warp(next, Board.Size))
                                {
                                    new_leads.Add(lead.Add(warp));
                                }
                            }
                            leads.Clear();
                            foreach (var lead in new_leads)
                            {
                                Piece piece = Board[lead.Value.P].Piece;
                                if (piece.Color == (Party)(-(int)king.Color)) continue;
                                if (piece.Color == king.Color && (piece.HasMoved ||
                                    piece.Value != PieceValue.Rook ||
                                    piece.Value != PieceValue.King ||
                                    (piece.Value == PieceValue.King && lead.Value.V == dir))) continue;
                                if (distance <= 2 && IsAttacked(lead.Value.P)) continue;
                                if (piece.Value == PieceValue.Rook) rook = piece;
                                if (distance == 1) {
                                    leads.Add(lead);
                                    continue;
                                }
                                if (rook != null) {
                                    //
                                }
                            }
                            new_leads.Clear();
                        } while (stepleads.Any() && distance++ < Board.Size * Board.Size);
                    }
                }
            }
            return moves;
        }

        public Dictionary<Cell, List<Move>> PossibleMoves { get; private set; } = new();

        public Dictionary<Cell, List<Move>> CalculateMoves()
        {
            Dictionary<Cell, List<Move>> moves = new();
            foreach (var piece in Players[CurrentParty].Pieces)
            {
                List<Move> piecemoves = PieceMoves(piece);
                if (piecemoves.Any()) moves.Add(Board[piece.Position], piecemoves);
            }
            return moves;
        }

        public void Play(Move move)
        {
            Piece piece = move.MovingPiece;
            piece.HasMoved = true;
            piece.RenderTransform.Matrix *= move.Path.Value.M;
            if (piece.Value == PieceValue.Pawn)
            {
                for (int i = 0; i < piece.MoveDirections.Length; i++)
                {
                    piece.MoveDirections[i] = (IntVector)move.Path.Value.M.Transform((Vector)piece.MoveDirections[i]);
                }
            }
            Piece captured = Board[move.Capture].Piece;
            Players[CurrentParty].Remove(captured);
            Board[move.Capture].Piece = Piece.Empty;
            Board[move.To].Piece = piece;
            Board[move.From].Piece = Piece.Empty;
            LastMove = move;
            History.Add(move);
            CurrentParty = (Party)(-(int)CurrentParty);

            PossibleMoves = CalculateMoves();
            if (!PossibleMoves.Any()) End();
        }

        public void End()
        {
            //
            MessageBox.Show("End");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
