import array

def sign_extend(x, bits):
    if (x >> (bits-1)) & 1:
        return (0xffff & (~((1 << bits) - 1))) | x
    return x

def condition(code, x):
    if code == 0: return x == 0
    elif code == 1: return x != 0
    elif code == 2: return x >= 0x8000
    elif code == 3: return x < 0x8000
    elif code == 4: return 0 < x < 0x8000
    elif code == 5: return x >= 0x8000 or x == 0
    else: raise NotImplemented

def alu(o, x, y):
    if o == 0: return x
    elif o == 1: return (x + y) & 0xffff
    elif o == 2: return (x - y) & 0xffff
    elif o == 3: return (x & y) & 0xffff
    elif o == 4: return (x | y) & 0xffff
    elif o == 5: return (x ^ y) & 0xffff
    elif o == 6: return (x << (y & 15) if y & 0x8000 == 0 else x >> (0x10 - (y & 15))) & 0xffff
    elif o == 7: return (x * y) & 0xffff

class CPU7:
    def __init__(self):
        self.ram = array.array('H', [0]*0x2000)
        self.ip = 0
        self.rp = 0x1fff
        self.sp = 0x1fbf
        self.reg = array.array('H', [0]*8)

    def port_in(self, p):
        pass

    def port_out(self, p, x):
        pass

    def rpush(self, x):
        self.rp -= 1
        self.ram[self.rp] = x

    def rpop(self):
        self.rp += 1
        return self.ram[self.rp-1]

    def ret(self):
        self.ip = self.rpop()

    def step(self):
        i = self.ram[self.ip]
        a = i & 7
        b = (i >> 3) & 7
        c = (i >> 6) & 7
        o = (i >> 10) & 7
        self.ip += 1
        if i & 0xe000 == 0x0000: # CALL
            self.rpush(self.ip)
            self.ip = i & 0x1fff
        elif i & 0xe000 == 0x2000: # BRANCH
            self.ip = i & 0x1fff
        elif i & 0xe000 == 0x4000: # CBRANCH
            if condition((i >> 3) & 7, self.reg[i & 7]):
                self.ip = (self.ip + sign_extend((i >> 6) & 0x7f, 7)) & 0xffff
        elif i & 0xe000 == 0x6000: # R0 <= X
            self.reg[0] = sign_extend(i & 1fff, 13)
        elif i & 0xe000 == 0x8000: # Ra <= ALU(Ra, x)
            x = sign_extend((i >> 3) & 0x3f, 6)
            self.reg[a] = alu(o, self.reg[a], x)
            if i & 0x0200: self.ret()
        elif i & 0xe000 == 0xa000: # Ra <= ALU(Rb, Rc)
            self.reg[a] = alu(o, self.reg[b], self.reg[c])
            if i & 0x0200: self.ret()
        elif i & 0xe000 == 0xc000: # [Ra] <= ALU(Rb, Rc)
            if i & 0x0200: self.reg[a] = (self.reg[a] - 1) & 0xffff
            self.ram[self.reg[a] & 0x1fff] = alu(o, self.reg[b], self.reg[c])
        elif i & 0xf000 == 0xe000: # Ra <= x
            x = sign_extend((i >> 3) & 0xff, 8)
            self.reg[a] = x
            if i & 0x0800: self.ret()
        elif i & 0xff80 == 0xf000: # Rb <= [Ra]
            self.reg[b] = self.ram[self.reg[a] & 0x1fff]
            if i & 0x0040: self.reg[a] = (self.reg[a] + 1) & 0xffff
        elif i & 0xff80 == 0xf080: # Ra <= PORT[Rb]
            self.reg[a] = self.port_in(self.reg[b])
            if i & 0x0040: self.ret()
        elif i & 0xff80 == 0xf100: # PORT[Ra] <= Rb
            self.port_out(self.reg[a], self.reg[b])
            if i & 0x0040: self.ret()

