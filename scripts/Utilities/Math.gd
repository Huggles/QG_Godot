class_name Math

static func sum_array(_array)->int:
    return _array.reduce(func(a,b):return a+b,0)
