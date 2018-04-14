using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace RX_THING
{
    public partial class MainWindow : Form
    {
        public MainWindow() {
            InitializeComponent();

            var debouncedButtonPressed = buttonPresses.Replay(1000);

            Observable.Create<string>(observer => {
                observer.OnNext("5");
                observer.OnNext("5");

                observer.OnNext("5");
                return debouncedButtonPressed.Subscribe(observer);
            });

            Observable.Return<string>("5");

            var myEnumerable = Enumerable.Range(0, 10);
            var stream = myEnumerable.ToObservable();
            stream.ToEnumerable();
            debouncedButtonPressed.ToEnumerable();


            debouncedButtonPressed.Connect();

            var onthis = debouncedButtonPressed.ObserveOn(this);

            onthis.Subscribe(buttonPress => listView1.Items.Add(buttonPress));
            Task.Delay(2000).ContinueWith(_ => {
                onthis.Subscribe(buttonPress => listView2.Items.Add(buttonPress));
            });
        }

        ISubject<string> buttonPresses = new Subject<string>();



        private void button1_Click(object sender, EventArgs e) {
            buttonPresses.OnNext("button1");
        }

        private void button2_Click(object sender, EventArgs e) {
            buttonPresses.OnNext("button2");
        }

        private void button3_Click(object sender, EventArgs e) {
            buttonPresses.OnNext("button3");
        }

        private void button4_Click(object sender, EventArgs e) {
            buttonPresses.OnNext("button4");
        }

    }
}
