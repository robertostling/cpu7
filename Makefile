cpu7.bin: cpu/cpu7.v cpu/rs232.v cpu/ice40hx8k-bb.pcf
	yosys -q -p "synth_ice40 -blif cpu7.blif" cpu/cpu7.v
	arachne-pnr -d 8k -p cpu/ice40hx8k-bb.pcf cpu7.blif -o cpu7.txt
	icepack cpu7.txt cpu7.bin
	rm -f cpu7.blif cpu7.txt cpu7.ex

upload-cpu: cpu7.bin
	iceprog -S cpu7.bin

kernel.hex: asm/kernel.fs asm/assembler.fs
	gforth asm/kernel.fs >kernel.hex

upload: kernel.hex cpu7.bin
	iceprog -S cpu7.bin && sleep 1
	python3 utils/upload_hex.py kernel.hex

clean:
	rm -f cpu7.blif cpu7.txt cpu7.ex cpu7.bin kernel.hex

