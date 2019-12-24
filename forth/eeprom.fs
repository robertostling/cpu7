: cs! ( x -- ) 0 iobit! ;
: clk ( -- ) 1 us  1 1 iobit!  1 us  0 1 iobit! ;
: si! ( x -- ) 2 iobit! ;
: so@ ( -- x ) 0 iobit@ ;
: init  1 cs!  1 us  0 cs! ;
: spi!  0 8 do  dup FFF9 shift  si!  clk  1 shift  loop  drop ;
: spiw!  lmsb spi! spi! ;
: spi@  0  0 8 do  1 shift  so@ or  clk  loop ;
: wren  init  06 spi!  1 cs!  1 us ;
: status ( -- x )  0 cs!  05 spi!  spi@  1 cs! ;
: wait-wr  begin  status  1 and  0= if  exit  then  again ;
: words@ ( a n e -- )  init  03 spi!  spiw!
    0 swap do
        spi@ 8 shift  spi@ or  over !  1+
    loop drop  1 cs! ;
: words! ( a n e -- )  wren  0 cs!  02 spi!  spiw!
    0 swap do
        dup @  spiw!  1+
    loop drop  1 cs! wait-wr ;
: block@ ( a b -- )  10 shift  200 swap  words@ ;
: block! ( a b -- )  10 shift
    0 20 do
        over over  10 swap  words!
        swap 10 +  swap 20 +
    loop drop drop ;
