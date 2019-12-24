\ Basic properties of the system
13                      constant address-bits
1 address-bits lshift   constant memory-words

\ Create memory buffer and initialize to zero
create memory  memory-words cells allot
memory memory-words cells erase

\ Address to assemble into, set to -1 here which will cause an error if the
\ user forgets to initialize it
variable org  -1 org !

: memory@ ( a -- )  cells memory + @ ;
: memory! ( x a -- )  cells memory + ! ;

: 'tip ( -- a ; get host address of next word to assemble )
  org @  0 memory-words within  0= if
    s" origin not set, or out of bounds" exception throw
  then
  org @ cells  memory + ;

: advance ( -- )  1 org +! ;

: ,word ( x -- )  'tip !  advance ;

\ String literal in code space, one character per word
\ First word is count
: words"  [char] " parse  dup ,word  0 do  dup c@ ,word  1+  loop drop ;

: 2** ( n -- 2**n )  1 swap lshift ;

: bitmask ( #bits -- mask )  2** 1- ;

: +bitfield ( x value start #bits -- x' )
  bitmask invert 2 pick and if
    s" value does not fit in bit field" exception throw
  then lshift or ;

: (un)signed ( x1 #bits -- x2 )  bitmask swap negate and negate ;

: address>signed ( a1 -- a2 )  13 (un)signed ;

: fits-signed ( x #bits -- t/f )
  1- 2**  dup negate swap within ;

: fits-unsigned ( x #bits -- t/f )
  2**  0 swap within ;

: >field ( x #bits fits? -- x' )
  0= if  s" value does not fit in literal" exception throw  then bitmask and ;

: signed>field ( x #bits -- x' )
  2dup fits-signed >field ;

: unsigned>field ( x #bits -- x' )
  2dup fits-unsigned >field ;

\ ------------------------------------------------------------------------
\ Words returning a complete instruction word

: <call> ( address -- i )
  0x0000 swap
  13 unsigned>field  0 13 +bitfield ;

: <r0!> ( literal -- i )
  0x6000 swap
  13 signed>field  0 13 +bitfield ;

: <branch> ( address -- i )
  0x2000 swap
  13 unsigned>field  0 13 +bitfield ;

: <cbranch> ( register condition a -- i )
  0x4000 swap
  7 signed>field    6 7 +bitfield swap
  3 unsigned>field  3 3 +bitfield swap
  3 unsigned>field  0 3 +bitfield ;

: <aluimm> ( register value op return -- i )
  0x8000 swap
  1 unsigned>field  9  1 +bitfield swap
  3 unsigned>field  10 3 +bitfield swap
  6 signed>field    3  6 +bitfield swap
  3 unsigned>field  0  3 +bitfield ;

: <alu> ( x register1 register2 op target-register return/store -- i )
  1 unsigned>field  9  1 +bitfield swap
  3 unsigned>field  0  3 +bitfield swap
  3 unsigned>field  10 3 +bitfield swap
  3 unsigned>field  6  3 +bitfield swap
  3 unsigned>field  3  3 +bitfield ;

: <alureg> ( register1 register2 op target-register return -- i )
  0xa000 swap <alu> ;

: <alustore> ( register1 register2 op target-register push/store -- i )
  0xc000 swap <alu> ;

: <r!> ( literal register return -- i )
  0xe000 swap
  1 unsigned>field  11 1 +bitfield swap
  3 unsigned>field   0 3 +bitfield swap
  8 signed>field     3 8 +bitfield ;

: <rfetch> ( source-register pop/fetch target-register -- i )
  0xf000 swap
  3 unsigned>field  3 3 +bitfield swap
  1 unsigned>field  6 1 +bitfield swap
  3 unsigned>field  0 3 +bitfield ;

: <port> ( source-register target-register return -- i )
  1 unsigned>field  6 1 +bitfield swap
  3 unsigned>field  0 3 +bitfield swap
  3 unsigned>field  3 3 +bitfield ;

: <port@> ( source-register target-register return -- i )
  0xf080 swap <port> ;

: <port!> ( source-register target-register return -- i )
  0xf100 swap <port> ;

\ ------------------------------------------------------------------------
\ Constant definitions

: enum ( n -- )  0 do  i constant  loop ;

\ Register names
8 enum r0 r1 r2 r3 r4 r5 r6 r7

\ Return flag
2 enum #noreturn #return

\ Flags for pre-decrement and post-increment addressing
2 enum #store #push
2 enum #fetch #pop

\ Condition codes (two sets of aliases)
6 enum #eq #neq #lt #ge #gt #le
6 enum #zero #nonzero #negative #nonnegative #positive #nonpositive

\ Used to be:
\ 6 enum #eq #neq #lt #gt #le #ge
\ 6 enum #zero #nonzero #negative #positive #nonpositive #nonnegative

\ ALU operations
8 enum #nop #add #sub #and #or #xor #shift #mul

\ ------------------------------------------------------------------------
\ High-level words for compiling machine instructions

: ''     ( -- address )  ' >body @ ;
: ::     ( -- )  create  org @ ,  does>  @ <call>  ,word ;
: goto   ( -- )  ''  <branch>  ,word ;
: ?goto  ( register condition -- )  ''  org @  -  <cbranch>  ,word ;
: r0!    ( value -- )  <r0!> ,word ;
: port@  ( source-register target-register -- )  #noreturn <port@> ,word ;
: port!  ( source-register target-register -- )  #noreturn <port!> ,word ;
: port@; ( source-register target-register -- )  #return <port@> ,word ;
: port!; ( source-register target-register -- )  #return <port!> ,word ;
: r!     ( literal register -- ) #noreturn <r!> ,word ;
: r!;    ( literal register -- ) #return <r!> ,word ;

\ Create a forward branch, good for a single use. Example:
\   forward future:
\       [... code ...]
\   future:
: forward   create  org @ ,  0 ,word  does>
    @  dup memory@ if
        s" forward reference already resolved" exception throw
    then  org @ <branch> swap memory! ;

: aluimm  ( op return -- )  create , , does>
  dup cell+ @  swap @  <aluimm> ,word ;
: alureg  ( op return -- )  create , , does>
  dup cell+ @  -rot @  <alureg> ,word ;

#add   #noreturn  aluimm +x
#sub   #noreturn  aluimm -x
#and   #noreturn  aluimm and-x
#or    #noreturn  aluimm or-x
#xor   #noreturn  aluimm xor-x
#shift #noreturn  aluimm shift-x
#mul   #noreturn  aluimm mul-x
#add   #return    aluimm +x;
#sub   #return    aluimm -x;
#and   #return    aluimm and-x;
#or    #return    aluimm or-x;
#xor   #return    aluimm xor-x;
#shift #return    aluimm shift-x;
#mul   #return    aluimm mul-x;

#nop   #noreturn  alureg <copy>
#nop   #return    alureg <copy;>
#add   #noreturn  alureg +r
#sub   #noreturn  alureg -r
#and   #noreturn  alureg and-r
#or    #noreturn  alureg or-r
#xor   #noreturn  alureg xor-r
#shift #noreturn  alureg shift-r
#mul   #noreturn  alureg mul-r
#add   #return    alureg +r;
#sub   #return    alureg -r;
#and   #return    alureg and-r;
#or    #return    alureg or-r;
#xor   #return    alureg xor-r;
#shift #return    alureg shift-r;
#mul   #return    alureg mul-r;

: copy ( source-register target-register -- )  r0 swap <copy> ;
: copy;  ( source-register target-register -- )  r0 swap <copy;> ;
: ;; ( -- ) r0 r0 copy; ;

: alustore ( op -- )  create , does>
    @ -rot <alustore> ,word ;

: @@ ( source-register pop/fetch target-register -- )
    <rfetch> ,word ;

#nop   alustore store
#add   alustore +store
#sub   alustore -store
#and   alustore and-store
#or    alustore or-store
#xor   alustore xor-store
#shift alustore shift-store

: !!  r0 -rot store ;

\ ------------------------------------------------------------------------
\ High-level control structure definitions

: opposite ( condition -- !condition )  1 xor ;

: _begin ( -- a )  org @ ;

: _until ( a register condition -- )  opposite rot org @ -  <cbranch> ,word ;

: _while ( a1 register condition -- register condition a1 a2 )
  opposite rot  org @  0 ,word ;

: _repeat ( register condition a1 a2 -- )
  swap  <branch> ,word      \ branch back to _begin
  dup >r                    \ save away address of conditional branch
  org @ swap - <cbranch>    \ compute conditional branch instruction
  r> memory! ;              \ write to slot reserved by _while

: _again ( a -- )  <branch> ,word ;

: _if ( register condition -- register condition a )
  opposite  org @  0 ,word ;

: _else ( register condition a1 -- -1 -1 a2 )
  org @ >r  0 ,word         \ will be changed to unconditional jump to _then
  dup >r  org @ swap - <cbranch>  r> memory!
  -1 -1 r> ;

: _then ( register condition a -- )
  over 0< if
    \ negative condition code indicates unconditional jump (from _else)
    \ in this case, compile unconditional branch here
    nip nip  org @ <branch>  swap
  else
    \ otherwise, we come straight from _if and should compile a conditional
    \ branch
    dup >r  org @ swap - <cbranch>  r>
  then 
  memory! ;

\ ------------------------------------------------------------------------
\ Double constants for pushing register and addressing type (with/without
\ pre-decrement/post-increment)

: 2enum ( x2 n -- )  0 do  i over  2constant  loop drop ;

#store 8 2enum [r0]  [r1]  [r2]  [r3]  [r4]  [r5]  [r6]  [r7]
#push  8 2enum [-r0] [-r1] [-r2] [-r3] [-r4] [-r5] [-r6] [-r7]
#pop   8 2enum [r0+] [r1+] [r2+] [r3+] [r4+] [r5+] [r6+] [r7+]

\ ------------------------------------------------------------------------
\ Hardware port definitions

2 enum port-serial-read port-serial-ready?
2 enum port-led port-serial-write

\ ------------------------------------------------------------------------
\ Output utilities

: dump-hex ( a n -- )
  base @ >r  hex
  over + swap do
    ( i . )  i cells memory + @ . cr
  loop
  r> base ! ;

: whole-program ( a -- a n )
  org @ over - ;

