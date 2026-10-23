// Purely decorative pitch lines (penalty boxes, six-yard boxes, halfway
// line, center circle) — sits behind the player rows so the pitch reads
// as an actual football pitch, with goalkeepers anchored in a real
// 18-yard box and strikers pushed up into the opposition's.
export function PitchMarkings() {
  return (
    <div className="pitch-markings">
      <div className="penalty-box top" />
      <div className="six-yard-box top" />
      <div className="center-circle" />
      <div className="halfway-line" />
      <div className="six-yard-box bottom" />
      <div className="penalty-box bottom" />
    </div>
  )
}
