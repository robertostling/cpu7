// clk = 12 MHz, 9600 baud
parameter BAUD_COUNT_BITS = 10;
parameter BAUD_COUNT = 625;

// clk = 12 MHz, 115200 baud
//parameter BAUD_COUNT_BITS = 6;
//parameter BAUD_COUNT = 52;


module rs232tx(
    input clk,
    input load,
    input[7:0] data,
    output reg tx,
    output ready);

    reg[(BAUD_COUNT_BITS+1)-1:0] baud_count;
    reg[3:0] count;
    reg[9:0] shift;

    assign ready = (count == 0);

    always @(posedge clk) begin
       if (baud_count == BAUD_COUNT*2 - 1) begin
            baud_count <= 0;
            if (count != 0 && count != 11) begin
                count <= count - 1;
                { shift[8:0], tx } <= shift[9:0];
            end
            else if (count == 11) begin
                count <= 10;
                tx <= 0;
            end
            else if (load && count == 0) begin
                count <= 11; // enter the waiting state
                shift[9:0] <= { 2'b11, data[7:0] };
            end
         end else begin
            baud_count <= baud_count + 1;
            if (load && count == 0) begin
                count <= 11; // enter the waiting state
                shift[9:0] <= { 2'b11, data[7:0] };
            end
         end
    end
endmodule


module rs232rx(
    input clk,
    input rx,
    output valid,
    output[7:0] data);

    reg[BAUD_COUNT_BITS-1:0] baud_count = 0;
    reg[4:0] count = 0;
    reg[9:0] shift = 0;

    assign data[7:0] = shift[8:1];

    wire baud_clk = (baud_count == (BAUD_COUNT-1));
    wire reading = (count != 0);
    wire valid = ((shift[0] == 0) && (shift[9] == 1) && ! reading);

    always @(posedge clk) begin
        baud_count <= (reading & ! baud_clk)? baud_count+1 : 0;

        if ((rx == 0) && (! reading))
            count <= 19;
        else if (baud_clk && reading) begin
            count <= count - 1;
            if (count[0])
                shift[9:0] <= { rx, shift[9:1] };
        end
    end
endmodule

