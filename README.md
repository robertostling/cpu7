# cpu7
CPU and Forth system for the iCE40-HX8K FPGA board

This is my first larger Verilog project, and first attempt at designing from
the logic level all the way up to a high-level programming system.

## Specifications

 * Hardware: 12 MHz 16-bit CPU, 8k words (16 kB) RAM, serial I/O.
 * Software: Currently a simple Forth system

## Requirements

 * [iCE40-HX8K breakout board](http://www.latticesemi.com/en/Products/DevelopmentBoardsAndKits/iCE40HX8KBreakoutBoard.aspx)
 * [IceStorm](http://www.clifford.at/icestorm/) (see link for dependencies)
 * [pySerial](https://pythonhosted.org/pyserial/) (Debian: `python3-serial`)
 * [gforth](https://www.gnu.org/software/gforth/) (Debian: `gforth`)

## Installing

Ensure that the dependencies above are installed, the iCE40-HX8K board is
connected to a USB port, with serial device `/dev/ttyUSB1` detected. Then
simply type:

    make upload

It will take about one minute to synthesize the CPU from Verilog. If
everything works, there should be no error messages. Check that everything
works by running

    utils/terminal.sh

Press enter, and you should see the following message:

    ** ready

Now you are interfacing the Forth kernel (`asm/kernel.fs`). Try defining a
simple word:

    : hi  space 48 emit 45 emit 4C dup emit emit 4F emit cr ;
    hi HELLO

Note that the defined vocabulary is very minimalistic. To get something a bit
more useful, please execute the following command on the CPU:

    3000 download eval

It is best then to exit the serial terminal, using `ctrl+]`. Then, on your
host system, execute:

    python3 utils/upload_text.py forth/core.fs 

This will copy the file `forth/core.fs` to memory starting at *byte* address
0x3000 (which corresponds to actual memory address 0x1800), and passing it to
the Forth interpreter. Log back in with `utils/terminal.sh` and verify that
everything works:

    : blink  0 10 do  FF 0 p!  1F4 ms  0 0 p!  1F4 ms  loop ;
    blink

Which should blink the on-board LED array 16 times. There is also an EEPROM
library in `forth/eeprom.fs` which I use to interface a 25AA640 chip.
This is an 8-pin SPI chip with 8 kB of EEPROM storage, 3.3V compatible.
It should be hooked up as follows:

    | 25AA640 pin | iCE40-HX8K header pin |
    | ----------- | --------------------- |
    | 1 (CS)      | C16                   |
    | 2 (SO)      | K14                   |
    | 5 (SI)      | E16                   |
    | 6 (SCK)     | D16                   |

Pins 3 (WP) and 7 (HOLD) are connected to Vpp through 10k resistors in order
to deactivate these features.

## CPU instruction set

There are 8 general-purpose registers, R0 to R7.
R7 is hard-coded as return stack pointer, and R6 as the top element of the
return stack.

The Forth system uses R5 as data stack pointer, and R4 to cache the top
element of that stack.

    000xxxxxxxxxxxxx    CALL x
    001xxxxxxxxxxxxx    BRANCH x
    010xxxxxxxzzzaaa    CBRANCH x
    011xxxxxxxxxxxxx    R0 <= x
    100ooorxxxxxxaaa    Ra <= ALU(Ra, x)        r = RET flag
    101ooorcccbbbaaa    Ra <= ALU(Rb, Rc)       r = RET flag
    110ooodcccbbbaaa    [Ra] <= ALU(Rb, Rc)     d = pre-dec Ra flag
    1110rxxxxxxxxaaa    Ra <= x                 r = RET flag
    111100000ibbbaaa    Rb <= [Ra]              i = post-inc Ra flag
    111100001rbbbaaa    Ra <= PORT[Rb]          r = RET flag
    111100010rbbbaaa    PORT[Ra] <= Rb          r = RET flag

Where `aaa`, `bbb`, `ccc` stand for register indexes (0 to 7). `xxxx...` is a
literal value, `r` is a return bit (1 = return after executing this
instruction), and `d`/`i` are flags for pre-decrement and post-increment
addressing, respectively.

Condition codes (marked `zzz` in the `CBRANCH` instruction) are as follows:

    Code  |  Name     | Name
    ----- | --------- | ----------
    000   | ZERO      | EQ
    001   | NONZERO   | NEQ
    010   | NEGATIVE  | LT
    011   | NONNEGATIVE | GE
    100   | POSITIVE    | GT
    101   | NONPOSITIVE | LE
    --    | undefined   |

ALU operations:

    Code | Operation
    ---- | ---------
    0    |  NOP
    1    |  ADD
    2    |  SUB
    3    |  AND
    4    |  OR
    5    |  XOR
    6    |  SHIFT
    7    |  MUL

Note that shifts are arithmetical, and to the left when the second operand is
positive, or to the right when it is negative.

## Assembler

A cross-assembler written in ANS Forth (tested with gforth) can be found in
`asm/assembler.fs`. See `asm/kernel.fs` for example usage. In typical Forth
style, this morphs into a Forth cross-compiler further down `kernel.fs`.

