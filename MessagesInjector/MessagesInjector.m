#import <Foundation/Foundation.h>
#import <sys/socket.h>
#import <sys/un.h>
#import <unistd.h>
#import <pthread.h>
#import <arpa/inet.h>
#import <sys/stat.h>

static NSString *const kSocketPath = @"/tmp/gamepigeonfucker-injector.sock";

@interface IMMessage : NSObject
- (instancetype)initWithSender:(id)sender
                           time:(id)time
                           text:(NSString *)text
                 messageSubject:(id)subject
              fileTransferGUIDs:(id)guids
                          flags:(unsigned long long)flags
                          error:(NSError **)error
                           guid:(NSString *)guid
                        subject:(id)subject2
                balloonBundleID:(NSString *)balloonBundleID
                    payloadData:(NSData *)payloadData
          expressiveSendStyleID:(id)styleID;
@end

@interface IMChat : NSObject
- (void)sendMessage:(IMMessage *)message;
@end

@interface IMChatRegistry : NSObject
+ (instancetype)sharedInstance;
- (IMChat *)existingChatWithGUID:(NSString *)guid;
@end

static NSDictionary *HandleRequest(NSDictionary *request)
{
    NSString *chatGuid = request[@"chatGuid"];
    NSString *text = request[@"text"];
    NSString *balloonBundleId = request[@"balloonBundleId"];
    NSString *payloadBase64 = request[@"payloadDataBase64"];

    NSData *payloadData = [payloadBase64 isKindOfClass:[NSString class]] && payloadBase64.length > 0
        ? [[NSData alloc] initWithBase64EncodedString:payloadBase64 options:0]
        : nil;

    IMChat *chat = [[IMChatRegistry sharedInstance] existingChatWithGUID:chatGuid];
    if (!chat)
    {
        return @{ @"ok": @NO, @"error": @"chat not found" };
    }

    NSError *error = nil;
    IMMessage *message = [[IMMessage alloc] initWithSender:nil
                                                        time:nil
                                                        text:text
                                              messageSubject:nil
                                           fileTransferGUIDs:nil
                                                       flags:100005
                                                       error:&error
                                                        guid:nil
                                                     subject:nil
                                             balloonBundleID:[balloonBundleId isKindOfClass:[NSString class]] && balloonBundleId.length > 0 ? balloonBundleId : nil
                                                 payloadData:payloadData
                                      expressiveSendStyleID:nil];
    if (!message)
    {
        return @{ @"ok": @NO, @"error": error.localizedDescription ?: @"failed to build message" };
    }

    [chat sendMessage:message];
    return @{ @"ok": @YES };
}

static void WriteAll(int fd, const void *buf, size_t len)
{
    size_t sent = 0;
    while (sent < len)
    {
        ssize_t n = write(fd, (const char *)buf + sent, len - sent);
        if (n <= 0)
        {
            return;
        }
        sent += (size_t)n;
    }
}

static NSData *ReadFrame(int fd)
{
    uint32_t length = 0;
    ssize_t n = read(fd, &length, sizeof(length));
    if (n != sizeof(length))
    {
        return nil;
    }

    length = ntohl(length);
    NSMutableData *data = [NSMutableData dataWithLength:length];
    size_t received = 0;
    while (received < length)
    {
        ssize_t r = read(fd, (uint8_t *)data.mutableBytes + received, length - received);
        if (r <= 0)
        {
            return nil;
        }
        received += (size_t)r;
    }

    return data;
}

static void WriteFrame(int fd, NSData *payload)
{
    uint32_t length = htonl((uint32_t)payload.length);
    WriteAll(fd, &length, sizeof(length));
    WriteAll(fd, payload.bytes, payload.length);
}

static void *AcceptLoop(void *arg)
{
    unlink(kSocketPath.UTF8String);

    int serverFd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (serverFd < 0)
    {
        return NULL;
    }

    struct sockaddr_un addr;
    memset(&addr, 0, sizeof(addr));
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, kSocketPath.UTF8String, sizeof(addr.sun_path) - 1);

    if (bind(serverFd, (struct sockaddr *)&addr, sizeof(addr)) != 0)
    {
        return NULL;
    }

    chmod(kSocketPath.UTF8String, 0600);

    if (listen(serverFd, 4) != 0)
    {
        return NULL;
    }

    while (1)
    {
        int clientFd = accept(serverFd, NULL, NULL);
        if (clientFd < 0)
        {
            continue;
        }

        @autoreleasepool
        {
            NSData *requestData = ReadFrame(clientFd);
            if (requestData)
            {
                NSDictionary *request = [NSJSONSerialization JSONObjectWithData:requestData options:0 error:nil];
                NSDictionary *response = [request isKindOfClass:[NSDictionary class]]
                    ? HandleRequest(request)
                    : @{ @"ok": @NO, @"error": @"bad request" };
                NSData *responseData = [NSJSONSerialization dataWithJSONObject:response options:0 error:nil];
                WriteFrame(clientFd, responseData);
            }
        }

        close(clientFd);
    }

    return NULL;
}

__attribute__((constructor))
static void MessagesInjectorInit(void)
{
    pthread_t thread;
    pthread_create(&thread, NULL, AcceptLoop, NULL);
}
