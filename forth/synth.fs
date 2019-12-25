0 var freq0l
0 var freq0h
0 var freq1l
0 var freq1h

: update-freq0  freq0h 4 p!  freq0l 5 p! ;
: update-freq1  freq1h 6 p!  freq1l 7 p! ;
: freq0! ( h l -- )  -> freq0l  -> freq0h  update-freq0 ;
: freq1! ( h l -- )  -> freq1l  -> freq1h  update-freq1 ;
: synth0-off  0 0 freq0! ;
: synth1-off  0 0 freq1! ;
: morse-char ( x n -- )
  0 swap do
    dup 1 and if C0 else 40 then beep  40 ms FFFF shift
  loop drop ;

