import React, { Component } from 'react';
import {
    BrowserRouter,
    Route,
    Link,
    Switch
} from 'react-router-dom'
import { Layout } from './components/Layout';
import { Schedule } from './components/Schedule';
import { Edit } from './components/Edit';


export default class App extends Component {
  displayName = App.name

  render() {
      return (
          <BrowserRouter>
              <Layout>
                  <Route exact path='/' component={Schedule}/>
                  <Route exact path='/schedule/:dateStr' component={Schedule}/>
                  <Route exact path="/edit/:id" component={Edit} />
                  <Route exact path="/new" component={Edit} />
                  <Route exact path="/new/:dateStr" component={Edit} />
              </Layout>
          </BrowserRouter>
      );
  }
}
