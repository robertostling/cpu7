`include "rs232.v"
`include "synth.v"

module main(
    //input wire resetq,
    input wire sysclk,
    input wire RS232_Rx,
    output wire RS232_Tx,
    output wire[7:0] LED,
    output wire[1:0] SYNTH,
    output wire[7:0] GPOUT,
    input wire[7:0] GPIN
    );

    parameter ADR_SIZE = 13,
              WORD_SIZE = 16,
              INIT_IP = 13'h0000;

    reg[63:0] cycles = 0;
    always @(posedge sysclk)
        cycles <= cycles + 1;


    wire pllclk;
    wire pll_locked;
    pll myPLL (.clock_in(sysclk), .global_clock(pllclk), .locked(pll_locked));

    reg[31:0] synth0scale;
    reg[31:0] synth1scale;

    synth synth0(.clk(pllclk), .outclk(SYNTH[0]), .scale(synth0scale));
    synth synth1(.clk(pllclk), .outclk(SYNTH[1]), .scale(synth1scale));

    reg[19:0] init_count = 0;
    //wire clk = sysclk && (init_count == 20'hfffff);
    always @(posedge sysclk)
        if (init_count != 20'hfffff)
            init_count <= init_count + 1;

    // Enough RAM to fill up the whole address space
    reg[WORD_SIZE-1:0] mem[0:(1<<ADR_SIZE)-1];

    wire reboot = (init_count != 20'hfffff);
    wire clk = sysclk;

    initial begin
// Simple bootloader for use with upload_hex.py
// Expects a sequence of MSB words, the first one being the number of data
// words. Following words will be written, starting at address 32.
// After transfer, it will jump to the loaded data block.
mem[0 ] <= 16'b0010000000001010;
mem[1 ] <= 16'b0110000000000000;
mem[2 ] <= 16'b1111000010000000;
mem[3 ] <= 16'b0101111110010000;
mem[4 ] <= 16'b1000001000000000;
mem[5 ] <= 16'b0000000000000001;
mem[6 ] <= 16'b1010000000000001;
mem[7 ] <= 16'b0000000000000001;
mem[8 ] <= 16'b1001100001000001;
mem[9 ] <= 16'b1011001001000000;
mem[10 ] <= 16'b0111111111111111;
mem[11 ] <= 16'b1010000000000111;
mem[12 ] <= 16'b0111111101111111;
mem[13 ] <= 16'b1010000000000101;
mem[14 ] <= 16'b0000000000000101;
mem[15 ] <= 16'b1010000000000010;
mem[16 ] <= 16'b0110000000100000;
mem[17 ] <= 16'b1010000000000011;
mem[18 ] <= 16'b0100001110000010;
mem[19 ] <= 16'b0000000000000101;
mem[20 ] <= 16'b1110000000000001;
mem[21 ] <= 16'b1111000100011001;
mem[22 ] <= 16'b1100000000000011;
mem[23 ] <= 16'b1000010000001011;
mem[24 ] <= 16'b1000100000001010;
mem[25 ] <= 16'b0010000000010010;
    end

    reg[ADR_SIZE-1:0] ip;
    reg[WORD_SIZE-1:0] r0, r1, r2, r3, r4, r5, r6, r7;

    wire[ADR_SIZE-1:0] ip_p1 = ip + 1;
    wire[ADR_SIZE-1:0] ip_plit = ip + {{6{rd[12]}}, rd[12:6]};
    wire[WORD_SIZE-1:0] r0_m1 = r0 - 1;
    wire[WORD_SIZE-1:0] r1_m1 = r1 - 1;
    wire[WORD_SIZE-1:0] r2_m1 = r2 - 1;
    wire[WORD_SIZE-1:0] r3_m1 = r3 - 1;
    wire[WORD_SIZE-1:0] r4_m1 = r4 - 1;
    wire[WORD_SIZE-1:0] r5_m1 = r5 - 1;
    wire[WORD_SIZE-1:0] r6_m1 = r6 - 1;
    wire[WORD_SIZE-1:0] r7_m1 = r7 - 1;
    wire[WORD_SIZE-1:0] r0_p1 = r0 + 1;
    wire[WORD_SIZE-1:0] r1_p1 = r1 + 1;
    wire[WORD_SIZE-1:0] r2_p1 = r2 + 1;
    wire[WORD_SIZE-1:0] r3_p1 = r3 + 1;
    wire[WORD_SIZE-1:0] r4_p1 = r4 + 1;
    wire[WORD_SIZE-1:0] r5_p1 = r5 + 1;
    wire[WORD_SIZE-1:0] r6_p1 = r6 + 1;
    wire[WORD_SIZE-1:0] r7_p1 = r7 + 1;

    //assign LED[7:0] = { state[2:0], ip[4:0] };
    //assign LED[7:0] = rs232_rxdata[7:0];
    reg[7:0] led_buf;
    assign LED[7:0] = (reboot)? init_count[19:12] : led_buf;

    // Latest data read from RAM
    reg[WORD_SIZE-1:0] rd;
    // Address to read from RAM
    wire[ADR_SIZE-1:0] ra;
    // Read Enable line
    wire re;

    // Data to write to RAM
    reg[WORD_SIZE-1:0] wd;
    // Address to write to RAM
    reg[ADR_SIZE-1:0] wa;
    // Write Enable line
    wire we;

    always @(posedge clk)
        if (re)
            rd <= mem[ra];

    always @(posedge clk)
        if (we)
            mem[wa] <= wd;

    reg[1:0] state;

    localparam STATE_FETCH          = 0,
               STATE_EXEC           = 1,
               STATE_LOAD           = 2,
               STATE_WRITE          = 3;

    wire state_fetch        = (state == STATE_FETCH);
    wire state_exec         = (state == STATE_EXEC);
    wire state_load         = (state == STATE_LOAD);
    wire state_write        = (state == STATE_WRITE);

    assign we = (! reboot) && state_write;
    assign re = (! reboot) && (! we);

    wire op_call        = rd[15:13] == 3'b000;
    wire op_branch      = rd[15:13] == 3'b001;
    wire op_cbranch     = rd[15:13] == 3'b010;
    wire op_r0imm       = rd[15:13] == 3'b011;
    wire op_aluimm      = rd[15:13] == 3'b100;
    wire op_alu         = rd[15:13] == 3'b101;
    wire op_aluwrite    = rd[15:13] == 3'b110;
    wire op_rimm        = rd[15:12] == 4'b1110;
    wire op_load        = rd[15:7] == 9'b111100000;
    wire op_portread    = rd[15:7] == 9'b111100001;
    wire op_portwrite   = rd[15:7] == 9'b111100010;

    localparam ALU_NOP           = 6'h00,
               ALU_ADD           = 6'h01,
               ALU_SUB           = 6'h02,
               ALU_AND           = 6'h03,
               ALU_OR            = 6'h04,
               ALU_XOR           = 6'h05,
               ALU_BITSHIFT      = 6'h06,
               ALU_MUL           = 6'h07;

    // the return bit are found in different places in different instructions
    wire i_r = (op_alu || op_aluimm)? rd[9] : 
                ((op_rimm)? rd[11] :
                    rd[6]);
    wire has_r =  op_alu || op_aluimm || op_rimm
               || op_portread || op_portwrite;

    wire[2:0] i_a = rd[2:0];    // operand register a
    wire[2:0] i_b = rd[5:3];    // operand register b
    wire[2:0] i_c = rd[8:6];    // operand register c
    wire[2:0] i_z = rd[5:3];    // condition code for op_cbranch
    wire[2:0] i_o = rd[12:10];  // ALU operation

    wire pre_dec = op_aluwrite && rd[9];
    wire post_inc = op_load && rd[6];

    // Plain and sign-extended versions of immediate constants
    wire[12:0] i_imm13 = rd[12:0];
    wire[15:0] i_imm13_to16 = {{3{i_imm13[12]}}, i_imm13};

    wire[5:0] i_imm6 = rd[8:3];
    wire[15:0] i_imm6_to16 = {{10{i_imm6[5]}}, i_imm6};
    
    wire[7:0] i_imm8 = rd[10:3];
    wire[15:0] i_imm8_to16 = {{8{i_imm8[7]}}, i_imm8};

    wire ra_is_0 = i_a == 3'h0;
    wire ra_is_1 = i_a == 3'h1;
    wire ra_is_2 = i_a == 3'h2;
    wire ra_is_3 = i_a == 3'h3;
    wire ra_is_4 = i_a == 3'h4;
    wire ra_is_5 = i_a == 3'h5;
    wire ra_is_6 = i_a == 3'h6;
    wire ra_is_7 = i_a == 3'h7;

    wire rb_is_0 = i_b == 3'h0;
    wire rb_is_1 = i_b == 3'h1;
    wire rb_is_2 = i_b == 3'h2;
    wire rb_is_3 = i_b == 3'h3;
    wire rb_is_4 = i_b == 3'h4;
    wire rb_is_5 = i_b == 3'h5;
    wire rb_is_6 = i_b == 3'h6;
    wire rb_is_7 = i_b == 3'h7;

    wire[2:0] read_port = alu_arg1[2:0];
    wire[2:0] write_port = adr_value[2:0];

    reg[7:0] gpout_buf = 0;
    assign GPOUT[7:0] = gpout_buf[7:0];

    reg[15:0] port_input;

    always @(*)
        case (read_port)
            3'h0: port_input = rs232_rxword;
            3'h1: port_input = rs232_readyword;
            3'h2: port_input = { 8'h0, GPIN[7:0] };
            3'h4: port_input = cycles[63:48];
            3'h5: port_input = cycles[47:32];
            3'h6: port_input = cycles[31:16];
            3'h7: port_input = cycles[15:0];
            default: port_input = 0;
        endcase

    reg[WORD_SIZE-1:0] alu_arg1;
    reg[WORD_SIZE-1:0] alu_arg2;
    reg[ADR_SIZE-1:0] adr_value;
    reg[WORD_SIZE-1:0] alu_out;
   
    always @(*)
        case ((op_aluimm || op_cbranch)? i_a : i_b)
            3'h0: alu_arg1 = r0;
            3'h1: alu_arg1 = r1;
            3'h2: alu_arg1 = r2;
            3'h3: alu_arg1 = r3;
            3'h4: alu_arg1 = r4;
            3'h5: alu_arg1 = r5;
            3'h6: alu_arg1 = r6;
            3'h7: alu_arg1 = r7;
        endcase
    
    always @(*)
        if (op_aluimm)
            alu_arg2 = i_imm6_to16;
        else case (i_c)
            3'h0: alu_arg2 = r0;
            3'h1: alu_arg2 = r1;
            3'h2: alu_arg2 = r2;
            3'h3: alu_arg2 = r3;
            3'h4: alu_arg2 = r4;
            3'h5: alu_arg2 = r5;
            3'h6: alu_arg2 = r6;
            3'h7: alu_arg2 = r7;
        endcase

    always @(*)
        case (i_a)
            3'h0: adr_value = r0[ADR_SIZE-1:0];
            3'h1: adr_value = r1[ADR_SIZE-1:0];
            3'h2: adr_value = r2[ADR_SIZE-1:0];
            3'h3: adr_value = r3[ADR_SIZE-1:0];
            3'h4: adr_value = r4[ADR_SIZE-1:0];
            3'h5: adr_value = r5[ADR_SIZE-1:0];
            3'h6: adr_value = r6[ADR_SIZE-1:0];
            3'h7: adr_value = r7[ADR_SIZE-1:0];
        endcase

    wire[ADR_SIZE-1:0] adr_value_write = (pre_dec)? adr_value-1 : adr_value;

    always @(*)
        case (i_o)
            ALU_NOP: alu_out = alu_arg1;
            ALU_ADD: alu_out = alu_arg1 + alu_arg2;
            ALU_SUB: alu_out = alu_arg1 - alu_arg2;
            ALU_AND: alu_out = alu_arg1 & alu_arg2;
            ALU_OR:  alu_out = alu_arg1 | alu_arg2;
            ALU_XOR: alu_out = alu_arg1 ^ alu_arg2;
            ALU_BITSHIFT: alu_out =
                (alu_arg2[15])? alu_arg1 >> (-alu_arg2[3:0])
                              : alu_arg1 << alu_arg2[3:0];
            ALU_MUL: alu_out = alu_arg1 * alu_arg2;
        endcase

    reg cond_true;

    always @(*)
        casez ({i_z, alu_arg1 == 0, alu_arg1[WORD_SIZE-1]})
            5'b000_1_0: cond_true = 1;      // EQ
            5'b001_0_?: cond_true = 1;      // NEQ
            5'b010_0_1: cond_true = 1;      // LT
            5'b100_0_0: cond_true = 1;      // GT
            5'b101_1_0: cond_true = 1;      // LE (1)
            5'b101_0_1: cond_true = 1;      // LE (2)
            5'b011_?_0: cond_true = 1;      // GE
            default:    cond_true = 0;
        endcase


    // true if instruction is a conditional branch which is taken
    wire cbranch_taken = op_cbranch && cond_true;
    // true if any branch (CALL, BRANCH, CBRANCH)
    wire branch_taken = op_call || op_branch || cbranch_taken;
    // instruction has a RET flag and it is set?
    wire ret_taken = has_r && i_r;

    // expressions to compute the IP of the current cycle, to be acted upon
    // during the next positive edge of clk
    wire[ADR_SIZE-1:0] cbranch_target = (cbranch_taken)? ip_plit : ip_p1;
    wire[ADR_SIZE-1:0] branch_target =
        (op_cbranch)? cbranch_target : i_imm13;
    wire[ADR_SIZE-1:0] next_ip =
        (! state_exec)? ip :                // IP only changed in STATE_EXEC
            ((branch_taken)? branch_target :// CALL, BRANCH, CBRANCH (taken)
                ((ret_taken)? r6 :          // RET
                    ip_p1));                // none of the above: IP += 1

    // Memory read address bus
    assign ra =
        (state_exec && op_load)? adr_value :
            ((state_exec && ret_taken)? r7[ADR_SIZE-1:0] :
                next_ip);

    // Flags indicating which register(s) to store result of memory read
    // during STATE_LOAD
    reg load_r0, load_r1, load_r2, load_r3, load_r4, load_r5, load_r6, load_r7;

    wire rs232_ready;
    wire[7:0] rs232_txdata = alu_arg1[7:0];
    wire rs232_load = op_portwrite && state_exec && write_port == 3'h1;
    wire[7:0] rs232_rxdata;
    wire rs232_valid;
    wire[WORD_SIZE-1:0] rs232_readyword =
        (rs232_ready)? 16'hffff : 16'h0000;
    reg rs232_waiting = 1;

    rs232rx _rx(
        .clk(sysclk),
        .rx(RS232_Rx),
        .valid(rs232_valid),
        .data(rs232_rxdata)
    );

    rs232tx _tx(
        .clk(sysclk),
        .load(rs232_load),
        .data(rs232_txdata),
        .tx(RS232_Tx),
        .ready(rs232_ready)
    );

    localparam RS232_BUFSIZE = 4;

    reg[7:0] rs232_buf[0:RS232_BUFSIZE-1];
    reg[1:0] rs232_buf_in = 0;
    reg[1:0] rs232_buf_out = 0;
    wire[1:0] rs232_buf_nextin = rs232_buf_in + 1;
    wire[1:0] rs232_buf_nextout = rs232_buf_out + 1;
    wire rs232_buf_full = rs232_buf_nextin == rs232_buf_out;
    wire rs232_buf_empty = rs232_buf_in == rs232_buf_out;
    wire[WORD_SIZE-1:0] rs232_rxword =
        (rs232_buf_empty)? 16'hffff : { 8'h00, rs232_buf[rs232_buf_out] };

    always @(posedge clk)
        if (reboot) begin
            rs232_waiting <= 1;
        end
        else if (state_exec && op_portread && read_port == 3'h0) begin
            if (! rs232_buf_empty)
                rs232_buf_out <= rs232_buf_nextout;
        end
        else if (rs232_waiting && rs232_valid && !rs232_buf_full)
        begin
            rs232_buf[rs232_buf_in] <= rs232_rxdata[7:0];
            rs232_buf_in <= rs232_buf_nextin;
            rs232_waiting <= 0;
	end
        else if (! rs232_valid)
            rs232_waiting <= 1;

    always @(posedge clk) begin
        if (reboot) begin
            state <= STATE_FETCH;
            ip <= INIT_IP;
            wa <= 0;
            wd <= 0;
            led_buf <= 8'b10100101;
        end
        else begin
            ip <= next_ip;

            if (state_fetch) begin
                // Instruction is being read into rd
                // This should only occur at boot
                state <= STATE_EXEC;
            end
            else if (state_load) begin
                if (load_r0) r0 <= rd;
                if (load_r1) r1 <= rd;
                if (load_r2) r2 <= rd;
                if (load_r3) r3 <= rd;
                if (load_r4) r4 <= rd;
                if (load_r5) r5 <= rd;
                if (load_r6) r6 <= rd;
                if (load_r7) r7 <= rd;
                state <= STATE_EXEC;
            end
            else if (state_write) begin
                // When this is executed, the next instruction should already
                // be in rd
                state <= STATE_EXEC;
            end
            else if (state_exec) begin
                // Decode and start executing instruction
                // rd contains the instruction word in this state, any
                // information needed in later states needs to be copied

                if (op_call) begin
                    state <= STATE_WRITE;
                    r6 <= {3'b000, ip_p1};
                    r7 <= r7_m1;
                    wa <= r7_m1;
                    wd <= r6;
                    // the logic for updating ip is found above
                end
                //if (op_branch) begin
                //    the logic for updating ip is found above
                //end
                else if (op_r0imm) begin
                    r0 <= i_imm13_to16;
                end
                //else if (op_cbranch) begin
                //    the logic for updating ip is found above
                //end
                else if (op_aluimm || op_alu) begin
                    if (ra_is_0) r0 <= alu_out;
                    if (ra_is_1) r1 <= alu_out;
                    if (ra_is_2) r2 <= alu_out;
                    if (ra_is_3) r3 <= alu_out;
                    if (ra_is_4) r4 <= alu_out;
                    if (ra_is_5) r5 <= alu_out;
                    if (ra_is_6) r6 <= alu_out;
                    if (ra_is_7) r7 <= alu_out;
                end
                else if (op_aluwrite) begin
                    // Subtract 1 if pre-dec flag is set, otherwise NOP
                    if (ra_is_0) r0[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_1) r1[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_2) r2[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_3) r3[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_4) r4[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_5) r5[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_6) r6[ADR_SIZE-1:0] <= adr_value_write;
                    if (ra_is_7) r7[ADR_SIZE-1:0] <= adr_value_write;
                    wa <= adr_value_write;
                    wd <= alu_out;
                    state <= STATE_WRITE;
                end
                else if (op_load) begin
                    // the logic for the read address is above
                    load_r0 <= rb_is_0;
                    load_r1 <= rb_is_1;
                    load_r2 <= rb_is_2;
                    load_r3 <= rb_is_3;
                    load_r4 <= rb_is_4;
                    load_r5 <= rb_is_5;
                    load_r6 <= rb_is_6;
                    load_r7 <= rb_is_7;
                    if (post_inc) begin
                        if (ra_is_0) r0 <= r0_p1;
                        if (ra_is_1) r1 <= r1_p1;
                        if (ra_is_2) r2 <= r2_p1;
                        if (ra_is_3) r3 <= r3_p1;
                        if (ra_is_4) r4 <= r4_p1;
                        if (ra_is_5) r5 <= r5_p1;
                        if (ra_is_6) r6 <= r6_p1;
                        if (ra_is_7) r7 <= r7_p1;
                    end
                    state <= STATE_LOAD;
                end
                else if (op_rimm) begin
                    if (ra_is_0) r0 <= i_imm8_to16;
                    if (ra_is_1) r1 <= i_imm8_to16;
                    if (ra_is_2) r2 <= i_imm8_to16;
                    if (ra_is_3) r3 <= i_imm8_to16;
                    if (ra_is_4) r4 <= i_imm8_to16;
                    if (ra_is_5) r5 <= i_imm8_to16;
                    if (ra_is_6) r6 <= i_imm8_to16;
                    if (ra_is_7) r7 <= i_imm8_to16;
                end
                else if (op_portwrite) begin
                    if (write_port == 3'h0) led_buf <= alu_arg1[7:0];
                    // write_port == 3'h1: serial write, code above
                    if (write_port == 3'h2) gpout_buf <=
                        (gpout_buf & ~alu_arg1[15:8]) |
                        (alu_arg1[7:0] & alu_arg1[15:8]);
                    if (write_port == 3'h4)
                        synth0scale[31:16] <= alu_arg1[15:0];
                    if (write_port == 3'h5)
                        synth0scale[15:0] <= alu_arg1[15:0];
                    if (write_port == 3'h6)
                        synth1scale[31:16] <= alu_arg1[15:0];
                    if (write_port == 3'h7)
                        synth1scale[15:0] <= alu_arg1[15:0];
                end
                else if (op_portread) begin
                    if (ra_is_0) r0 <= port_input;
                    if (ra_is_1) r1 <= port_input;
                    if (ra_is_2) r2 <= port_input;
                    if (ra_is_3) r3 <= port_input;
                    if (ra_is_4) r4 <= port_input;
                    if (ra_is_5) r5 <= port_input;
                    if (ra_is_6) r6 <= port_input;
                    if (ra_is_7) r7 <= port_input;
                end
                if (ret_taken) begin
                    r7 <= r7_p1;
                    load_r0 <= 0;
                    load_r1 <= 0;
                    load_r2 <= 0;
                    load_r3 <= 0;
                    load_r4 <= 0;
                    load_r5 <= 0;
                    load_r6 <= 1;
                    load_r7 <= 0;
                    state <= STATE_LOAD;
                end
            end
        end
    end
endmodule

