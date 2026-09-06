TARGET = libMessagesInjector.dylib
FRAMEWORKS = -F/System/Library/PrivateFrameworks -framework IMCore -framework Foundation

all: $(TARGET)

$(TARGET): MessagesInjector.m
	clang -dynamiclib -fobjc-arc $(FRAMEWORKS) -o $(TARGET) MessagesInjector.m

clean:
	rm -f $(TARGET)
