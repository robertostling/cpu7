import time
import serial

if __name__ == '__main__':
    import sys

    with open(sys.argv[1]) as f:
        words = [int(s, 16) for s in f.read().split()]

    data = [len(words)] + words
    sequence = []
    for word in data:
        sequence.append((word >> 8) & 0xff)
        sequence.append(word & 0xff)

    device = '/dev/ttyUSB1'
    ser = serial.Serial(device, baudrate=9600, timeout=1)

    print('Writing %d bytes to %s' % (len(sequence), device))
    #print('Writing byte sequence:', ' '.join(('%02x' % x) for x in sequence))
    ser.write(bytes(sequence))
    ser.close()

