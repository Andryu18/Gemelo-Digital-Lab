//Digital twin of the RC car steering (situation 3)
//Serial protocol: text, 9600 baud, one message per line ending in '\n'
//  Unity   -> Arduino  A:<angle>          digital command, integer 0 to 180
//  Arduino -> Unity    S:<angle>,<source> state report, source is POT or DIG
//Arbitration: after an A: command the knob is released until it crosses the target

#include <Servo.h>

//Pins
const byte pot_pin = A0;
const byte servo_pin = 9;

//Angle limits
const int protocol_max = 180;
const int angle_min = 45;
const int angle_max = 135;

//Servo speed in degrees per second
const float max_speed = 120.0;

//Potentiometer dead zone in degrees
const int dead_zone = 2;

//Task periods in milliseconds
const unsigned long pot_period = 10;
const unsigned long servo_period = 20;
const unsigned long report_period = 50;

//Servo state
Servo servo;
float current_angle;
int target_angle;

//Potentiometer state
float filtered_pot;
int pot_angle;
bool knob_has_control = true;

//Task timers
unsigned long pot_time, servo_time, report_time;

//Serial input buffer
char buffer[12];
byte length = 0;
bool discarding = false;

void setup() {
  //Serial start
  Serial.begin(9600);
  pinMode(LED_BUILTIN, OUTPUT);

  //Initial potentiometer reading
  filtered_pot = analogRead(pot_pin);
  pot_angle = pot_to_angle(filtered_pot);
  target_angle = pot_angle;
  current_angle = target_angle;

  //Servo start
  servo.attach(servo_pin);
  servo.write(target_angle);

  //Timers start
  pot_time = servo_time = report_time = millis();
}

void loop() {
  unsigned long now = millis();

  //Command reception
  read_serial();

  //Potentiometer task
  if (now - pot_time >= pot_period) {
    pot_time = now;
    read_pot();
  }

  //Servo task
  if (now - servo_time >= servo_period) {
    move_servo(now - servo_time);
    servo_time = now;
  }

  //Report task
  if (now - report_time >= report_period) {
    report_time += report_period;
    send_report();
  }
}

//Potentiometer synchronization
void read_pot() {
  //Low pass filter
  filtered_pot = 0.8 * filtered_pot + 0.2 * analogRead(pot_pin);
  int reading = pot_to_angle(filtered_pot);

  //Dead zone
  if (abs(reading - pot_angle) >= dead_zone) {
    //Knob captures the target when the difference changes sign
    if (!knob_has_control && (pot_angle - target_angle) * (reading - target_angle) <= 0) {
      knob_has_control = true;
    }
    pot_angle = reading;
    if (knob_has_control) target_angle = reading;
  }

  //LED on means knob released
  digitalWrite(LED_BUILTIN, knob_has_control ? LOW : HIGH);
}

//Potentiometer value to servo angle
int pot_to_angle(float value) {
  return map((long)value, 0, 1023, angle_min, angle_max);
}

//Progressive servo movement
void move_servo(unsigned long dt) {
  //Degrees allowed in this interval
  float step = max_speed * dt / 1000.0;
  float diff = target_angle - current_angle;

  if (fabs(diff) <= step) current_angle = target_angle;
  else current_angle += (diff > 0) ? step : -step;

  servo.write((int)round(current_angle));
}

//State report
void send_report() {
  Serial.print("S:");
  Serial.print((int)round(current_angle));
  Serial.print(',');
  Serial.println(knob_has_control ? "POT" : "DIG");
}

//Non blocking serial read
void read_serial() {
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n') {
      //End of line
      if (!discarding) {
        buffer[length] = '\0';
        process_command(buffer);
      }
      length = 0;
      discarding = false;
    } else if (c == '\r') {
      //Ignore carriage return
    } else if (length < sizeof(buffer) - 1) {
      buffer[length++] = c;
    } else {
      //Line too long is malformed
      discarding = true;
    }
  }
}

//Command validation
void process_command(const char* text) {
  //Format check
  if (text[0] != 'A' || text[1] != ':' || text[2] == '\0') return;

  //Digits only and range 0 to 180
  long value = 0;
  for (const char* p = text + 2; *p; p++) {
    if (*p < '0' || *p > '9') return;
    value = value * 10 + (*p - '0');
    if (value > protocol_max) return;
  }

  //Limit to the useful steering range
  target_angle = constrain((int)value, angle_min, angle_max);

  //Release the knob until it crosses the target
  knob_has_control = false;
}
