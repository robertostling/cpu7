import time
import serial

if __name__ == '__main__':
    import sys

    sequence = bytes()
    for filename in sys.argv[1:]:
        with open(filename, 'rb') as f:
            sequence = sequence + f.read()

    length = len(sequence)

    sequence = bytes([(length >> 8) & 0xff, length & 0xff]) + sequence

    device = '/dev/ttyUSB1'
    ser = serial.Serial(device, baudrate=9600, timeout=1)

    print('Writing %d bytes to %s...' % (len(sequence), device))
    ser.write(sequence)
    text = ser.read(100000)
    print(str(text, 'utf-8'))
    ser.close()

