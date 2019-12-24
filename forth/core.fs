: (does>)  r>  dict @ >cfa  ! ;
: does>  (lit) (does>) ,  (lit) (does) , ; immediate
: negate  0 swap - ;
: if  (lit) ?nbranch ,  here 0 , ; immediate
: else  (lit) branch ,  here swap  0 ,  here swap ! ; immediate
: then  here swap ! ; immediate
: begin  here ; immediate
: again  (lit) branch ,  , ; immediate
: do  (lit) (do) ,  here ; immediate
: loop  (lit) (loop) ,  , ; immediate
: skip-until  begin read-char over = if  drop exit  then  again [
: skip-while  begin
    peek-char over <> if  drop exit  then  read-char drop
  again [
: literal  (lit) (lit) ,  , ; immediate
: [char]  20 skip-while  read-char ; immediate
: (  [char] ) literal  skip-until ; immediate
: dump ( a n -- ) cr begin
    ?dup 0= if  drop exit  then  swap dup @ . 1+ swap
  1- again [
: ms ( n -- ) begin  ?dup 0= if  exit  then  03E7 us 1-  again [
: ctype ( ba n -- )  over + do  i c@ emit  loop ;
: lmsb ( x -- lsb msb )  dup  FF and swap  FFF8 shift FF and ;
: iobit! ( x bit -- )  swap 1 and 0100 or  swap shift  2 p! ;
: iobit@ ( bit -- x )  2 p@  swap negate  shift  1 and ;
: variable  create , ;
: var  create ,  does> @ ;
: lit  r> r> dup @ -rot 1+ >r >r ;
: ->  lit 1+ ! ;

