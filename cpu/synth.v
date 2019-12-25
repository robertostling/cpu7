// Apparently from a more recent verison of the icepll tool:
// https://mjoldfield.com/atelier/2018/02/ice40-blinky-hx8k-breakout.html
module pll(
    input  clock_in,
    output global_clock,
    output locked
    );

   wire g_clock_int;
   wire g_lock_int;
    
   SB_PLL40_CORE #(
                .FEEDBACK_PATH("SIMPLE"),
                .DIVR(4'b0000),         // DIVR =  0
                .DIVF(7'b0111111),      // DIVF = 63
                .DIVQ(3'b011),          // DIVQ =  3
                .FILTER_RANGE(3'b001)   // FILTER_RANGE = 1
        ) uut (
                .LOCK(g_lock_int),
                .RESETB(1'b1),
                .BYPASS(1'b0),
                .REFERENCECLK(clock_in),
                .PLLOUTGLOBAL(g_clock_int)
                );

   SB_GB clk_gb ( .USER_SIGNAL_TO_GLOBAL_BUFFER(g_clock_int)
                  , .GLOBAL_BUFFER_OUTPUT(global_clock) );

   SB_GB lck_gb ( .USER_SIGNAL_TO_GLOBAL_BUFFER(g_lock_int)
                  , .GLOBAL_BUFFER_OUTPUT(locked) );
endmodule

module synth(
    input clk,
    output outclk,
    input[31:0] scale
    );

    reg[31:0] count;

    assign outclk = (scale[31:0] == 32'h0000)? 0 : count[31];

    always @(posedge clk)
        count <= count + scale;
endmodule

