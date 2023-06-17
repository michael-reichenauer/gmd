// // https://en.wikipedia.org/wiki/List_of_Unicode_characters
// // Foreground text colors
// const (
// 	lineBranch           = line('┃')
// 	lineBranchCommit     = line('┠')
// 	lineBranchBegin      = line('╂')
// 	lineBranchEnd        = line('┸')
// 	lineBranchPass       = line('╂')
// 	lineLine             = line('│')
// 	LineLineCommit       = line('┼')
// 	lineLinePass         = line('┼')
// 	lineOutRight         = line('╯')
// 	lineOutRightCommit   = line('╯')
// 	lineInRight          = line('╮')
// 	lineInRightCommit    = line('╮')
// 	lineOutInRightCommit = line('┼')
// 	lineOutLeftCommit    = line('╰')
// 	lineInLeftCommit     = line('╭')
// 	lineNone             = line(' ')
// )

// type lp struct {
// 	isBranch      bool // '┃' or '│'
// 	isToRight     bool // '┠'
// 	isToLeft      bool // '┨'
// 	isNoDown      bool // '┸'
// 	isToRightUp   bool // '╯'
// 	isToRightDown bool // '╮'
// 	isToLeftUp    bool // '╰'
// 	isToLeftDown  bool // '╭'
// 	isMiddle      bool // '─'

// }

// const (
// 	// i := 0x2500; i <= 0x257F;
// 	lines = `
// ─ ━ │ ┃ ┄ ┅ ┆ ┇ ┈ ┉ ┊ ┋ ┌ ┍ ┎ ┏
// ┐ ┑ ┒ ┓ └ ┕ ┖ ┗ ┘ ┙ ┚ ┛ ├ ┝ ┞ ┟
// ┠ ┡ ┢ ┣ ┤ ┥ ┦ ┧ ┨ ┩ ┪ ┫ ┬ ┭ ┮ ┯
// ┰ ┱ ┲ ┳ ┴ ┵ ┶ ┷ ┸ ┹ ┺ ┻ ┼ ┽ ┾ ┿
// ╀ ╁ ╂ ╃ ╄ ╅ ╆ ╇ ╈ ╉ ╊ ╋ ╌ ╍ ╎ ╏
// ═ ║ ╒ ╓ ╔ ╕ ╖ ╗ ╘ ╙ ╚ ╛ ╜ ╝ ╞ ╟
// ╠ ╡ ╢ ╣ ╤ ╥ ╦ ╧ ╨ ╩ ╪ ╫ ╬ ╭ ╮ ╯
// ╰ ╱ ╲ ╳ ╴ ╵ ╶ ╷ ╸ ╹ ╺ ╻ ╼ ╽ ╾ ╿
// `
// ▲ ▼

// 	points = `
//  Φ Ф * ⊙ ⊛ ├ ∤ ∘ o
// `

//  Φ Ф * ⓿ Ѻ 0 O Θ Ѳ Ӂ Ӝ Ⴔ ቐ ቿ ቾ ቶ ፬ ∅ *
// ◙ □
// ▣
// ▢ ▣
// ☐ ☑ ☒ ⊠
// □ ■ 
// _ _ 



// 	used = `
// ●
// ⓿
// Ѻ
// █
// ▓
// ▒
// ─ 
// ┠
// ┃
// ╋
// ┻
// ┒
// ┚
// │
// ┐
// └
// ┴
// ┌
// ┘
// ┤
// ├
// `
// 	Repov1 = `
// ┠┬      Merge branch 'branches/newFeat' into develop (1)
// ┃└──┰ * Some more cleaning (2)
// ┠┬  ┃   Merge branch 'branches/diff' into develop (1)
// ┃└┰ ┃   Adjust commitVM diff (3)
// ┃┌┸ ┃   Update git to 2.23 (3)
// ┃│  ┠   Fixing a bug (2)
// ┃│ ┌┸   Clean code (2)
// █┴─┘    Merge branch 'branches/branchcommit' into develop (1)
// ┠       fix tag names with strange char ending (1)
// ┠       Clean build script (1)
// ┠┐      Merge branch 'branches/NewBuild' into develop (1)
// ┃└┰     Update some cake tools (4)
// ┃┌┸     Add Cake build script support (4)
// ┠┼      Merge branch 'branches/FixIssues' into develop (1)
// ┃└┰     Adjust file monitor logging (5)
// ┃ ┠     Adjust expected git version (5)
// ┃ ┠     Use git 2.20.0  (5)
// ┃ ┠     Clean diff temp files (5)
// ┃┌┺     Fix missing underscore char in details file list (5)
// ┠┴      Version 0.144 (1)
// ┠       Some text  (1)
// `

// 	Repov = `
// ┏╮      Merge branch 'branches/newFeat' into develop (1)
// ┃╰┲     Some more cleaning (2)
// ┣─╂╮    Merge branch 'branches/diff' into develop (1)
// ┃ ┃╰┲   Adjust commitVM diff (3)
// ┃ ┃╭┺  ╸Update git to 2.23 (3)
// ┃ ┣│    Fixing a bug (2)
// ┃╭┺│   │Clean code (2)
// ┣┴─╯  + Merge branch 'branches/branchcommit' into develop (1)
// ┣       fix tag names with strange char ending (1)
// ┣       Clean build script (1)
// ┣╮      Merge branch 'branches/NewBuild' into develop (1)
// ┃╰┲     Update some cake tools (4)
// ┃╭┺     Add Cake build script support (4)
// ┣┼      Merge branch 'branches/FixIssues' into develop (1)
// ┃╰┲     Adjust file monitor logging (5)
// ┃ ┣     Adjust expected git version (5)
// ┃ ┣     Use git 2.20.0  (5)
// ┃ ┣     Clean diff temp files (5)
// ┃╭┺     Fix missing underscore char in details file list (5)
// ┣╯      Version 0.144 (1)
// ┗       Some text  (1)

// `
// )
