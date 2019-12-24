\ Minimal serial boot loader included in Verilog initialization code

require assembler.fs

32 org !

:: user-code


0 org !

    forward start:

:: read-byte
    port-serial-read r0!
    r0 r0 port@
    r0 #lt ?goto read-byte
    ;;

:: read-word
    read-byte
    r0 r1 copy
    read-byte
    r1 8 shift-x
    r0 r1 r0 or-r;

start:

:: main
    0x1fff address>signed r0!
    r0 r7 copy
    0x1f7f address>signed r0!
    r0 r5 copy

    read-word
    r0 r2 copy
    '' user-code r0!
    r0 r3 copy
:: copy-next
    r2 #eq ?goto user-code
    read-word
    0 r1 r!
    r3 r1 port!
    r0 [r3] !!
    r3 1 +x
    r2 1 -x
    goto copy-next


0 whole-program dump-hex
bye

