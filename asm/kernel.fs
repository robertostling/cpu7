require assembler.fs

32 org !
    forward start:

\ This should point to memory somewhere after the end of the kernel
0x540 constant heap-start

r4      constant tos
r5      constant sp
r6      constant rtos
r7      constant rp
[r4]    2constant [tos]
[r4+]   2constant [tos+]
[r5]    2constant [sp]
[-r5]   2constant [-sp]
[r5+]   2constant [sp+]
[r6]    2constant [rtos]
[r6+]   2constant [rtos+]
[-r6]   2constant [-rtos]
[r7]    2constant [rp]
[-r7]   2constant [-rp]
[r7+]   2constant [rp+]


\ Naming conventions:
\
\ dup       host system operation (assembler)
\ _dup      host system macros for generating target system code
\ *dup      target system subroutines
\
\ Exceptions:
\ :: x      define subroutine (using x compiles a call)
\ ;;        return (compiles no-op with return bit set)
\ '' x      push target address of subroutine x to host stack


\ Single-instruction primitives for inlining
\ ------------------------------------------------------------------------
: _push ( H: reg -- )  [-sp] !! ;
: _pop  ( H: reg -- )  [sp+] rot @@ ;

: _rpush ( H: reg -- )  [-rp] !! ;
: _rpop  ( H: reg -- )  [rp+] rot @@ ;

: _rdup   rtos _rpush ;
: _rdrop  rtos _rpop ;

: _dup  ( H: -- ) tos _push ;
: _drop ( H: -- ) tos _pop ;
: _drop_dup ( H: x y -- x x )  [sp] tos @@ ;
: _nip  ( H: -- ) sp 1 +x ;
: _nip; ( H: -- ) sp 1 +x; ;

: _@  ( a -- x )  [tos] tos @@ ;
: _p@ ( a -- x )  tos tos port@ ;

: _1+   tos 1 +x ;
: _1+;  tos 1 +x; ;
: _1-   tos 1 -x ;
: _1-;  tos 1 -x; ;
: _2+   tos 2 +x ;
: _2-   tos 2 -x ;

\ ------------------------------------------------------------------------

\ Convert between words and byte addresses/sizes
: _w>b  tos 1 shift-x ;
:: *b>w
    tos 1 +x
    tos -1 shift-x;

:: *lit ( -- x )
    _dup
    [rtos+] tos @@
    ;;

: _literal ( x -- )
  dup -128 128 within if
    \ small constants can be written directly to TOS
    _dup
    tos r!
  else
    \ larger constants use a compiled
    [ '' *lit ] literal ,word  ,word
  then ;

:: *(doconst) ( -- x )
    _dup
    [rtos] tos @@
    _rdrop
    ;;

:: *(dovar) ( -- a )
    _dup
    rtos tos copy
    _rdrop
    ;;

: _constant ( x -- )
    create  org @ ,  *(doconst) ,word
        does>  @ <call> ,word ;

: _create ( -- )
    create  org @ ,  *(dovar)
        does>  @ <call> ,word ;

: _variable ( x -- )  _create  ,word ;

\ Version when using *(dovar) above:
: _'  ' >body @ 1+ ; immediate


\ Older versions that inlined literals:
\ : _create  create  org @ ,  does>  @ _literal ;
\ : _variable  _create  ,word ;
\ : _constant  create ,  does>  @ _literal ;
\ Version when inlining:
\ : _'  ' >body @ ; immediate


heap-start _variable *heap

: _here  ( -- a )   _' *heap literal  memory@ ;
: _allot ( n -- )   _' *heap literal  tuck memory@ +  swap memory! ;
: _array ( n -- )  _here swap _allot
    _constant ( -- a ) ;

: w>b  2* ;
: b>w  1+ 2/ ;

: _barray ( n -- )  _here swap b>w _allot
    w>b _constant ( -- ba ) ;

0 _variable *state-compile
0 _variable *dictionary
31 _constant *parse-buffer-size
32 _array *parse-buffer

: header"
    [char] " parse
    org @
    _' *dictionary literal ( a n org 'dict )
    dup memory@ ,word   \ pointer to last header
    memory!             \ *dictionary now points here
    0 ,word             \ flags (none set for now)
    dup ,word           \ length of name
    0 do
        dup c@ ,word  1+
    loop
    drop ;

: _immediate
    _' *dictionary literal memory@ 1+
    dup memory@ 1 or  swap memory! ;


\ Same as *lit above, but easier to define here with header"
header" (lit)"
:: *(lit) ( -- x )
    _dup
    [rtos+] tos @@
    ;;

header" +!"
:: *+! ( x a -- )
    tos r0 copy
    _drop
    [r0] r1 @@
    tos r1 tos +r
    tos [r0] !!
    _drop
    ;;

header" (does)"
:: *(does)
    _dup
    [rp+] tos @@
    ;;

header" sp@"
:: *sp@
    _dup
    sp tos copy;

header" rp@"
:: *rp@
    _dup
    rp tos copy
    tos 1 -x;

header" dict"
:: *dict  goto *dictionary

header" here"
:: *here ( -- a ) *heap _@ ;;

\ header" here!"
\ :: *here ( -- a ) *heap goto *!

header" allot"
:: *allot ( n -- ) *heap *+! ;;

header" dup"
:: *dup ( x -- x x )
    _dup ;;

header" drop"
:: *drop ( x -- )
    _drop ;;

\ header" 2drop"
\ :: *2drop ( x y -- )
\     _drop _drop ;;

header" nip"
:: *nip ( x y -- y )
    _nip;

header" swap"
:: *swap ( x y -- y x ; destroys r0 )
    tos r0 copy
    [sp] tos @@
    r0 [sp] !!
    ;;

header" over"
:: *over ( x y -- x y x ; destroys r0 )
    [sp] r0 @@
    _dup
    r0 tos copy;

header" rot"
:: *rot ( x y z -- y z x ; destroys r0 )
    tos r2 copy     \ r2 = z
    sp r0 copy
    [r0] r1 @@      \ r1 = y
    r2 [r0] !!      \ write z
    r0 1 +x
    [r0] tos @@     \ tos = x
    r1 [r0] !!      \ write y
    ;;

header" -rot"
:: *-rot ( x y z -- z x y )
    *rot
    goto *rot

header" >r"
:: *>r
    tos [-rp] !!
    _drop
    ;;

header" r>"
:: *r>
    _dup
    [rp+] tos @@
    ;;

header" !"
:: *! ( x a -- )
    [sp+] r0 @@
    r0 [tos] !!
    _drop
    ;;

header" @"
:: *@ ( a -- x ) _@ ;;


\ Load byte from byte-based address
header" c@"
:: *c@ ( ba -- c )
    tos r1 copy
    r1 -1 shift-x
    [r1] r2 @@
    tos 1 and-x
    tos #zero _if
        r2 -8 shift-x
    _then
    0xff r0!
    r2 r0 tos and-r;

\ Store byte to byte-based address
header" c!"
:: *c! ( c ba -- )
    0xff r0!
    tos r1 copy
    r1 -1 shift-x
    tos 1 and-x
    [sp+] r2 @@
    r2 r0 r2 and-r
    tos #nonzero _if
        r0 8 shift-x
    _else
        r2 8 shift-x
    _then
    [r1] tos @@
    tos r0 r0 and-r
    r2 r0 r0 or-r
    r0 [r1] !!
    goto *drop

header" ,"
:: *, ( x -- ) *here *!  1 _literal *allot ;;

header" p!"
:: *p! ( x p -- )
    tos r0 copy
    _drop
    tos r0 port!
    _drop
    ;;

header" p@"
:: *p@ ( p -- x )
    tos tos port@;

header" execute"
:: *execute  *>r ;;

header" -"
:: *- ( x y -- x-y )
    tos r0 copy
    _drop
    tos r0 tos -r;

header" +"
:: *+ ( x y -- x+y )
    tos r0 copy
    _drop
    tos r0 tos +r;

header" and"
:: *and ( x y -- x&y )
    tos r0 copy
    _drop
    tos r0 tos and-r;

header" or"
:: *or ( x y -- x|y )
    tos r0 copy
    _drop
    tos r0 tos or-r;

header" xor"
:: *xor ( x y -- x^y )
    tos r0 copy
    _drop
    tos r0 tos xor-r;

header" *"
:: *mul ( x y -- x*y )
    tos r0 copy
    _drop
    tos r0 tos mul-r;

header" shift"
:: *shift ( x y -- x<<y or x>>-y )
    tos r0 copy
    _drop
    tos r0 tos shift-r;

header" >cfa"
:: *>cfa ( a1 -- a2 )
    _2+ _dup _@ *+ _1+;

: _comparison ( condition -- )
    ::  *-  tos swap _if  -1 tos r!;  _then  0 tos r!; ;

header" <"  #lt _comparison *<
header" <=" #le _comparison *<=
header" >"  #gt _comparison *>
header" >=" #ge _comparison *>=
header" ="  #eq _comparison *=
header" <>" #neq _comparison *<>

header" 0="
:: *0=  0 _literal *= ;;

header" 0<>"
:: *0<>  0 _literal *<> ;;

header" within"
:: *within ( x min max+1 -- f )
    *-rot *over         ( max+1 x min x )
    *<=                 ( max+1 x f1 )
    *-rot               ( f1 max+1 x )
    *>                  ( f1 f2 )
    goto *and


header" move"
:: *move ( src dest n -- )
    tos r1 copy                 \ r1 = n
    _drop
    tos r2 copy                 \ r2 = dest
    _drop                       \ tos = src
    _begin
        r1 #zero _if
            _drop ;;
        _then
        [tos+] r0 @@
        r0 [r2] !!
        r2 1 +x
        r1 1 -x
    _again

header" key?"
:: *key?  port-serial-read _literal _p@ ;;

header" key"
:: *key
:: serial-read-byte ( -- x )
    _begin
        *key?
    tos #negative _while
        _drop
    _repeat
    ;;

header" emit"
:: *emit
:: serial-write-byte ( x -- ; destroys r0 )
    port-serial-ready? r0 r!
    r0 r0 port@
    r0 #zero ?goto serial-write-byte
    port-serial-write r0 r!
    tos r0 port!
    _drop
    ;;

header" cr"
:: *cr  10 _literal goto *emit

header" space"
:: *space  32 _literal goto *emit


:: *hexdigit ( x -- c )
    _dup 10 _literal *-
    tos #negative _if
        _drop char 0 _literal *+ ;;
    _then
    _nip char A _literal *+ ;;

header" ."
:: *.h ( x -- )
    *space
    4 _literal
    _begin
        *over -12 _literal *shift 15 _literal *and
        *hexdigit *emit
        *swap 4 _literal *shift
        *swap _1-
    tos #zero _until
    _drop _drop
    ;;


\ TODO: prevent user from writing longer lines!
128 _barray *edit-buffer         \ buffer data, two characters/word
128 _constant *edit-buffer-size  \ size of buffer in characters
0 _variable *edit-cursor         \ position of cursor, relative to buffer

:: *edit-insert ( c -- )
    *edit-cursor _@  *edit-buffer *+  *c!
    1 _literal  *edit-cursor  goto *+!
 
header" edit"
:: *edit ( -- )
    0 _literal  *edit-cursor *!
    *cr
    _begin
        *key
        _dup 10 _literal *=   tos #nonzero _if
            _drop _drop 
            32 _literal  goto *edit-insert
        _then
        _drop_dup 8 _literal *=  tos #nonzero _if
            _drop_dup *emit *space *emit
            -1 _literal  *edit-cursor *+!
        _else
            _drop_dup *emit
            *edit-insert
        _then
    _again

8 _array *input-stack
0 _variable *input-stack-pointer

header" eval"
:: *push-input ( ba size -- )
    *over *+ *swap                  ( a-end a-start )
    *input-stack-pointer _@  *input-stack *+
    *swap *over *!                  \ field 0: start address
    _1+ *!                          \ field 1: end address + 1
    2 _literal  *input-stack-pointer  goto *+!

:: *drop-input ( -- )
    *input-stack-pointer _@  tos #nonzero _if
        -2 _literal  *input-stack-pointer *+!
    _then
    _drop
    ;;

:: *push-edit ( -- )
    _begin
        *edit
        *edit-cursor _@  tos #nonzero _if
            *edit-buffer *swap  goto *push-input
        _then
    _again

:: *normalize-char ( c1 -- c2 )
    _dup  10 _literal *=  tos #nonzero _if
        _drop  32 tos r!;
    _then
    goto *drop

header" peek-char"
:: *peek-char ( -- c )
    *input-stack-pointer _@  tos #zero _if
        _drop
        *push-edit                  \ last resort: read from terminal
        *input-stack-pointer _@
    _then
    *input-stack *+
    _2- _@ *c@
    goto *normalize-char

header" read-char"
:: *read-char ( -- c )
    *peek-char
    *input-stack-pointer _@  *input-stack *+
    _dup _2-  1 _literal  *over *+! ( c stack-pointer 'cur )
    _@  *over _1- _@                ( c stack-pointer cur+1 end+1 )
    *=  tos #nonzero _if
        *drop-input
    _then
    _drop _drop ;;

:: *skip-spaces ( -- )
    _begin
        *peek-char
        32 _literal *-  tos #neq _if
            _drop ;;
        _then
        _drop
        *read-char _drop
    _again

:: *read-done
    _drop
    r2 tos copy;        \ return number of bytes actually read
:: *read ( a n1 c -- n2 ; destroys r0 r1 r2 r3 )
    tos r3 copy         \ r3 = termination character
    _drop
    tos r0 copy         \ r0 = buffer size
    _drop
    tos r1 copy         \ r1 = buffer pointer
    0 r2 r!             \ r2 = number of items read
    _begin
        r0 _rpush
        r1 _rpush
        r2 _rpush
        r3 _rpush
        *read-char
        r3 _rpop
        r2 _rpop
        r1 _rpop
        r0 _rpop
        tos [r1] !!
        r1 1 +x         \ [r1++] = new item
        r2 1 +x
        tos r3 tos -r   \ termination character?
        tos #eq ?goto *read-done
        r0 tos copy
        tos r2 tos -r   \ out of buffer space?
        tos #eq ?goto *read-done
        _drop
    _again

header" word"
    goto *parse-buffer

header" parse"
:: *parse ( c -- )
    *skip-spaces
    *parse-buffer _1+ *parse-buffer-size *rot  *read
    _1-
    *parse-buffer *!
    ;;

:: *uncount ( a -- a+1 n )  _dup _1+ *swap _@ ;;

:: *digit? ( c -- f )
    _dup  char 0 _literal  char 9 1+ _literal  *within
    *swap char A _literal  char F 1+ _literal  *within
    goto *or

header" number?"
:: *number? ( a -- f )
    *uncount
    _begin
        tos #zero _if
            _nip  -1 tos r!;
        _then
        _1- *swap
        _dup _@  *digit?
        tos #zero _if
            _nip _nip;
        _then
        _drop _1+ *swap
    _again

header" digit"
:: *digit ( c -- x )
    char A _literal  *-
    tos #negative _if
        char A  char 0 -  _literal  goto *+
    _then
    10 _literal  goto *+

header" number"
:: *number ( a -- x )
    *uncount                ( a+1 x n )
    0 _literal *swap
    _begin
        tos #zero _if
            _drop _nip;
        _then
        _1- *-rot           ( n-1 a+1 x )
        tos 4 shift-x       ( n-1 a+1 16*x )
        *over _@ *digit *+  ( n-1 a+1 x' )
        *swap _1+ *swap     ( n-1 a+2 x' )
        *rot                ( a+2 x' n-1 )
    _again

:: *@compare+ ( a1 a2 -- a1+1 a2+1 f )
    *over _@ *over _@ *-  *rot _1+ *rot _1+ *rot ;;

header" compare"
:: *compare ( a1 a2 -- f )
    _dup _@ *-rot
    _begin                                  ( n-left a1 a2 )
        *@compare+                          ( n-left a1 a2 f )
        tos #nonzero _if
            _drop _drop _drop  0 tos r!;    ( -- 0 )
        _then
        _drop                               ( n-left a1 a2 )
        *rot  tos #zero _if                 ( a1 a2 n-left )
            _drop _drop  -1 tos r!;         ( -- -1 )
        _then
        _1- *-rot                           ( n-left a1 a2 )
    _again

\ :: *depth  _dup  sp tos copy  *.h *cr ;;

header" type"
:: *type ( a n -- )
    _begin
        tos #zero _if  _drop _drop ;;  _then
        _1- *swap
        _dup _@ *emit _1+
        *swap
    _again

header" lookup"
:: *lookup ( a1 -- a2|0 )
    *dictionary
    _begin
        _@                                  ( str header )
        tos #zero _if _nip;  _then          ( -- 0 )
        *over *over  _2+  *compare          ( str header f )
        tos #nonzero _if  _drop _nip;  _then
        _drop                               ( str header )
    _again

header" ["  _immediate
:: *[  0 _literal  *state-compile  *! ;;

header" ]"
:: *]  -1 _literal  *state-compile  *! ;;

:: *unknown
    *space *parse-buffer *uncount *type char ? _literal *emit *cr
    *[
    goto *drop-input

header" '"
:: *'  32 _literal *parse  *parse-buffer *lookup
    tos #zero _if
        _drop
        goto *unknown
    _then
    goto *>cfa

header" immediate"
:: *immediate
    *dictionary _@ _1+  _dup _@ 1 _literal *or  *swap *! ;;

header" create"
:: *create
    32 _literal *parse              \ read word
    *here
    *dictionary _@ *,
    *dictionary *!                  \ link back to previous header
    0 _literal *,                   \ flags
    *parse-buffer *uncount
    _dup *,                         \ store count
    _begin
        *over _@ *,                 \ store one character
        *swap _1+ *swap _1-
    tos #le _until
    _drop _drop
    '' *(dovar) _literal *,         \ compile (dovar)
    ;;

header" branch"
:: *branch
    [rtos] rtos @@
    ;;

header" ?nbranch"
:: *?nbranch
    tos #zero _if
        _drop
        goto *branch
    _then
    _drop
    rtos 1 +x
    ;;

header" ?branch"
:: *?branch
    tos #nonzero _if
        _drop
        goto *branch
    _then
    _drop
    rtos 1 +x
    ;;

header" (loop)"
:: *(loop)
    [rp+] r0 @@
    [rp] r1 @@
    rp 1 -x
    r0 1 +x
    r0 [rp] !!
    r1 r0 r1 -r
    r1 #zero _if
        rp 2 +x
        rtos 1 +x
        ;;
    _then
    goto *branch

header" i"
:: *i
    _dup
    [rp] tos @@
    ;;

header" (do)"
:: *(do)
    tos [-rp] !!
    _drop
    tos [-rp] !!
    goto *drop

header" ?dup"
:: *?dup  tos #nonzero _if  _dup  _then ;;

\ header" if" _immediate
\ :: *if
\     '' *?nbranch _literal *,
\     *here
\     0 _literal *,
\     ;;
\ 
\ header" then" _immediate
\ :: *then  *here *swap *! ;;
\ 
\ header" else" _immediate
\ :: *else
\     '' *branch _literal *,
\     *here *swap
\     0 _literal *,
\     *here *swap *!
\     ;;

header" exit"
:: *exit  _rdrop ;;

\ :: *lsb ( x -- y )  0xff _literal goto *and
\ :: *msb ( x -- y )  -8 _literal *shift goto *lsb

header" 1+"
:: *1+  _1+;

header" 1-"
:: *1-  _1-;

:: *serial-word ( -- x ) *key 8 _literal *shift *key *or ;;

header" download"
:: *download ( ba -- ba n )
    *serial-word *over *over
    _begin
        tos #zero _if
            _drop _drop ;;
        _then
        *swap
        *key  *over *c!  _1+
        *swap _1-
    _again


_create *welcome-msg words" **** ready"

header" :"
:: *:  *create  -1 _literal *allot  *] ;;

header" ;"  _immediate
:: *;   r0 r0 #nop r0 #return <alureg>  _literal *,   *[ ;;

header" us"
:: *us  ( n -- )
    _begin
        tos #zero _if
            _drop ;;
        _then           \ 1 cycle
        *dup            \ 5 cycles
        *drop           \ 9 cycles
        *1-             \ 11 cycles
    _again              \ 12 cycles

header" welcome"
:: *welcome  *welcome-msg *uncount *type *cr ;;

start:
    *key *welcome
    _begin
        32 _literal *parse
        *parse-buffer *number?
        tos #nonzero _if
            _drop *parse-buffer *number
            *state-compile _@  tos #nonzero _if
                _drop '' *lit _literal *, *,
            _else
                _drop
            _then
        _else
            _drop
            *parse-buffer *lookup
            tos #zero _if
                _drop
                *unknown
            _else
                _dup _1+ _@ 1 _literal *and  *0=    \ not immediate?
                *state-compile _@  *and             \ and also in compile mode
                *swap *>cfa *swap
                tos #nonzero _if
                    _drop  *,                       \ both true, then compile
                _else
                    _drop  *execute                 \ otherwise interpret
                _then
            _then
        _then
    _again


: check-heap
    org @  heap-start  >  if
        s" Please adjust heap-start!" exception throw
    then ;

check-heap

32 whole-program dump-hex
bye

