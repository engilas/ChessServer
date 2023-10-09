module ChessServer.Common.Types.Domain

type Color = White | Black

type PieceType =
| King
| Queen
| Rook
| Bishop
| Knight
| Pawn

type Move = {
    Src: byte
    Dst: byte
}

type MoveDescription = { 
    Primary: Move
    TakenPiecePos: byte option
    PawnPromotion: PieceType option
    Check: bool
    Mate: bool
}